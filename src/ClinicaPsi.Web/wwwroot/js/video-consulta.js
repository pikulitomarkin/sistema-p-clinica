/**
 * Videochamada 1:1 WebRTC + SignalR (sem conta em provedor externo).
 * Perfect negotiation (polite/impolite), fila de ICE, STUN+TURN, auto-reconnect.
 */
(function (global) {
  'use strict';

  // Fallback se a página não injetar iceServers (produção injeta coturn via Video.cshtml).
  const DEFAULT_ICE_SERVERS = [
    { urls: 'stun:stun.l.google.com:19302' },
    { urls: 'stun:stun1.l.google.com:19302' }
  ];

  function log() {
    if (!global.console) return;
    var args = ['[VideoConsulta]'].concat(Array.prototype.slice.call(arguments));
    console.log.apply(console, args);
  }

  function VideoConsultaClient(options) {
    this.consultaId = options.consultaId;
    this.roomName = options.roomName;
    this.displayName = options.displayName;
    this.role = options.role;
    this.remoteNameDefault = options.remoteNameDefault || 'Participante';
    this.hubUrl = options.hubUrl || '/hubs/video-consulta';
    this.iceServers = options.iceServers || DEFAULT_ICE_SERVERS;
    this.onMediaConnected = typeof options.onMediaConnected === 'function' ? options.onMediaConnected : null;
    this.onStarted = typeof options.onStarted === 'function' ? options.onStarted : null;

    this.localVideo = options.localVideo;
    this.remoteVideo = options.remoteVideo;
    this.statusEl = options.statusEl;
    this.localNameEl = options.localNameEl;
    this.remoteNameEl = options.remoteNameEl;
    this.peerStatusEl = options.peerStatusEl;
    this.remotePlaceholder = options.remotePlaceholder || null;

    this.connection = null;
    this.pc = null;
    this.localStream = null;
    this.peerConnectionId = null;
    this.makingOffer = false;
    this.ignoreOffer = false;
    this.isSettingRemoteAnswer = false;
    // Paciente/Cliente é "polite" (cede no glare); Psicólogo/Admin é impolite
    this.polite = options.role !== 'Psicologo' && options.role !== 'Admin';
    this._started = false;
    this._pendingIce = [];
    this._negotiateTimer = null;
    this._iceRestartTimer = null;
    this._mediaConnected = false;
  }

  VideoConsultaClient.prototype.setStatus = function (text, kind) {
    if (!this.statusEl) return;
    this.statusEl.textContent = text;
    this.statusEl.className = 'video-status alert mb-3 alert-' + (kind || 'info');
  };

  VideoConsultaClient.prototype.setRemoteName = function (name) {
    if (this.remoteNameEl) this.remoteNameEl.textContent = name || this.remoteNameDefault;
  };

  VideoConsultaClient.prototype._setPeerStatus = function (text) {
    if (this.peerStatusEl) this.peerStatusEl.textContent = text;
  };

  VideoConsultaClient.prototype._showRemotePlaceholder = function (show) {
    if (!this.remotePlaceholder) return;
    if (show) this.remotePlaceholder.classList.remove('hidden');
    else this.remotePlaceholder.classList.add('hidden');
  };

  VideoConsultaClient.prototype._attachRemoteTrack = function (ev) {
    if (!this.remoteVideo) return;
    var stream = (ev.streams && ev.streams[0]) || null;
    if (!stream) {
      stream = this.remoteVideo.srcObject;
      if (!(stream instanceof MediaStream)) {
        stream = new MediaStream();
      }
      stream.addTrack(ev.track);
    }
    this.remoteVideo.srcObject = stream;
    var self = this;
    this.remoteVideo.play().then(function () {
      self._showRemotePlaceholder(false);
    }).catch(function (err) {
      log('remote play()', err);
      // Mesmo sem autoplay, esconde placeholder se houver track ao vivo
      if (ev.track && ev.track.readyState === 'live') self._showRemotePlaceholder(false);
    });
    this._setPeerStatus('Em chamada');
    this._mediaConnected = true;
    this.setStatus('Vídeo conectado com ' + (this.remoteNameEl ? this.remoteNameEl.textContent : this.remoteNameDefault), 'success');
    if (this.onMediaConnected) this.onMediaConnected();
    log('ontrack', ev.track && ev.track.kind, 'streams=', ev.streams && ev.streams.length);
  };

  VideoConsultaClient.prototype.start = async function () {
    if (this._started) return;
    this._started = true;

    if (this.localNameEl) this.localNameEl.textContent = this.displayName;
    this.setRemoteName(this.remoteNameDefault);
    this.setStatus('Solicitando câmera e microfone…', 'info');

    try {
      this.localStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        },
        video: {
          facingMode: 'user',
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      });
      if (this.localVideo) {
        this.localVideo.srcObject = this.localStream;
        this.localVideo.muted = true;
        await this.localVideo.play().catch(function () {});
      }
      log('getUserMedia ok', this.localStream.getTracks().map(function (t) { return t.kind + ':' + t.readyState; }));
    } catch (err) {
      console.error(err);
      this.setStatus('Não foi possível acessar câmera/microfone. Verifique as permissões do navegador.', 'danger');
      this._started = false;
      throw err;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this._wireHub();

    this.setStatus('Conectando à sala…', 'info');
    await this.connection.start();
    log('SignalR connected', this.connection.connectionId);
    await this.connection.invoke('JoinRoom', this.consultaId, this.roomName, this.displayName, this.role);
    if (this.onStarted) this.onStarted();
  };

  VideoConsultaClient.prototype._wireHub = function () {
    var self = this;

    this.connection.on('RoomJoined', function (payload) {
      var peers = (payload && payload.peers) || [];
      log('RoomJoined peers=', peers.length, peers);
      if (peers.length === 0) {
        self.setStatus('Na sala. Aguardando o outro participante…', 'info');
        self._setPeerStatus('Aguardando…');
        return;
      }
      var peer = peers[0];
      self._onPeerPresent(peer.connectionId, peer.displayName, 'já está na sala');
    });

    this.connection.on('PeerJoined', function (peer) {
      log('PeerJoined', peer);
      self._onPeerPresent(peer.connectionId, peer.displayName, 'entrou na sala');
    });

    this.connection.on('IncomingCall', function (peer) {
      log('IncomingCall', peer);
      self.peerConnectionId = peer.connectionId;
      self.setRemoteName(peer.displayName);
      self.setStatus(peer.displayName + ' está chamando…', 'warning');
      self._ensurePeerConnection();
      self._scheduleNegotiate(300);
    });

    this.connection.on('PeerLeft', function (peer) {
      log('PeerLeft', peer);
      if (self.peerConnectionId && peer.connectionId !== self.peerConnectionId) return;
      self.setStatus((peer.displayName || 'Participante') + ' saiu da sala.', 'warning');
      self._setPeerStatus('Desconectado');
      self._showRemotePlaceholder(true);
      self._closePeerConnection(false);
      self.peerConnectionId = null;
      self._mediaConnected = false;
    });

    this.connection.on('ReceiveOffer', async function (msg) {
      try {
        log('ReceiveOffer from', msg.fromConnectionId, 'state=', self.pc && self.pc.signalingState);
        self.peerConnectionId = msg.fromConnectionId;
        if (msg.displayName) self.setRemoteName(msg.displayName);
        self._ensurePeerConnection();

        var offerCollision = self.makingOffer || self.pc.signalingState !== 'stable';
        self.ignoreOffer = !self.polite && offerCollision;
        if (self.ignoreOffer) {
          log('glare: ignorando oferta (impolite)');
          return;
        }

        if (offerCollision && self.polite) {
          log('glare: polite faz rollback');
          try {
            await self.pc.setLocalDescription({ type: 'rollback' });
          } catch (rbErr) {
            log('rollback falhou, recriando PC', rbErr);
            self._closePeerConnection(false);
            self._ensurePeerConnection();
          }
        }

        await self.pc.setRemoteDescription({ type: 'offer', sdp: msg.sdp });
        await self._flushPendingIce();

        var answer = await self.pc.createAnswer();
        await self.pc.setLocalDescription(answer);
        await self.connection.invoke('SendAnswer', msg.fromConnectionId, answer.sdp);
        self.setStatus('Chamada em andamento com ' + (msg.displayName || self.remoteNameDefault), 'success');
        self._setPeerStatus('Conectando…');
        log('Answer enviada');
      } catch (e) {
        console.error('ReceiveOffer', e);
        self.setStatus('Falha ao aceitar a chamada. Tentando novamente…', 'danger');
        self._scheduleNegotiate(800);
      }
    });

    this.connection.on('ReceiveAnswer', async function (msg) {
      try {
        log('ReceiveAnswer state=', self.pc && self.pc.signalingState);
        if (!self.pc) return;
        if (self.pc.signalingState !== 'have-local-offer') {
          log('Answer ignorada — estado', self.pc.signalingState);
          return;
        }
        self.isSettingRemoteAnswer = true;
        await self.pc.setRemoteDescription({ type: 'answer', sdp: msg.sdp });
        await self._flushPendingIce();
        self.setStatus('Conectado com ' + (msg.displayName || self.remoteNameDefault), 'success');
        log('Answer aplicada');
      } catch (e) {
        console.error('ReceiveAnswer', e);
      } finally {
        self.isSettingRemoteAnswer = false;
      }
    });

    this.connection.on('ReceiveIceCandidate', async function (msg) {
      try {
        if (!msg.candidate) return;
        var cand = typeof msg.candidate === 'string' ? JSON.parse(msg.candidate) : msg.candidate;
        if (!cand) return;
        if (!self.pc || !self.pc.remoteDescription) {
          self._pendingIce.push(cand);
          log('ICE enfileirado (sem remoteDescription ainda)', self._pendingIce.length);
          return;
        }
        await self.pc.addIceCandidate(cand);
      } catch (e) {
        if (!self.ignoreOffer) console.warn('ICE candidate', e);
      }
    });

    this.connection.onreconnecting(function () {
      self.setStatus('Reconectando sinalização…', 'warning');
    });

    this.connection.onreconnected(async function () {
      self.setStatus('Sinalização reconectada. Reentrando na sala…', 'info');
      self._closePeerConnection(false);
      self.peerConnectionId = null;
      self._mediaConnected = false;
      try {
        await self.connection.invoke('JoinRoom', self.consultaId, self.roomName, self.displayName, self.role);
      } catch (e) {
        console.error(e);
      }
    });
  };

  VideoConsultaClient.prototype._onPeerPresent = function (connectionId, displayName, reason) {
    this.peerConnectionId = connectionId;
    this.setRemoteName(displayName);
    this._setPeerStatus('Online');
    this.setStatus(displayName + ' ' + reason + '. Conectando vídeo…', 'success');
    this._ensurePeerConnection();
    // Impolite inicia na hora; polite também tenta após breve atraso (se oferta não chegar)
    if (!this.polite) {
      this._scheduleNegotiate(100);
    } else {
      this._scheduleNegotiate(1200);
    }
  };

  VideoConsultaClient.prototype._scheduleNegotiate = function (delayMs) {
    var self = this;
    if (this._negotiateTimer) clearTimeout(this._negotiateTimer);
    this._negotiateTimer = setTimeout(function () {
      self._negotiateTimer = null;
      self._createAndSendOffer(false);
    }, delayMs || 150);
  };

  VideoConsultaClient.prototype._flushPendingIce = async function () {
    if (!this.pc || !this._pendingIce.length) return;
    var queued = this._pendingIce.splice(0, this._pendingIce.length);
    log('Aplicando', queued.length, 'ICE pendentes');
    for (var i = 0; i < queued.length; i++) {
      try {
        await this.pc.addIceCandidate(queued[i]);
      } catch (e) {
        console.warn('flush ICE', e);
      }
    }
  };

  VideoConsultaClient.prototype._ensurePeerConnection = function () {
    if (this.pc) return;
    var self = this;
    this._pendingIce = [];
    this._mediaConnected = false;

    this.pc = new RTCPeerConnection({
      iceServers: this.iceServers,
      iceCandidatePoolSize: 4
    });
    log('RTCPeerConnection criada', 'polite=', this.polite);

    if (this.localStream) {
      this.localStream.getTracks().forEach(function (track) {
        self.pc.addTrack(track, self.localStream);
      });
    }

    this.pc.onnegotiationneeded = function () {
      log('negotiationneeded state=', self.pc && self.pc.signalingState);
      // Impolite conduz; polite só se ainda sem mídia após atraso
      if (!self.polite) self._scheduleNegotiate(50);
    };

    this.pc.ontrack = function (ev) {
      self._attachRemoteTrack(ev);
    };

    this.pc.onicecandidate = function (ev) {
      if (!ev.candidate || !self.peerConnectionId || !self.connection) return;
      self.connection.invoke('SendIceCandidate', self.peerConnectionId, JSON.stringify(ev.candidate.toJSON()))
        .catch(function (e) { console.warn(e); });
    };

    this.pc.oniceconnectionstatechange = function () {
      var st = self.pc && self.pc.iceConnectionState;
      log('iceConnectionState', st);
      if (st === 'connected' || st === 'completed') {
        self._setPeerStatus('Em chamada');
        if (self.remoteVideo && self.remoteVideo.srcObject) self._showRemotePlaceholder(false);
      } else if (st === 'failed') {
        self.setStatus('Falha na conexão de mídia. Reiniciando ICE…', 'warning');
        self._restartIce();
      } else if (st === 'disconnected') {
        self.setStatus('Conexão instável. Aguardando restabelecer…', 'warning');
        if (self._iceRestartTimer) clearTimeout(self._iceRestartTimer);
        self._iceRestartTimer = setTimeout(function () {
          if (self.pc && self.pc.iceConnectionState === 'disconnected') self._restartIce();
        }, 4000);
      }
    };

    this.pc.onconnectionstatechange = function () {
      var st = self.pc && self.pc.connectionState;
      log('connectionState', st);
      if (st === 'connected') {
        self.setStatus('Conectado com ' + (self.remoteNameEl ? self.remoteNameEl.textContent : self.remoteNameDefault), 'success');
        self._setPeerStatus('Em chamada');
        if (self.remoteVideo && self.remoteVideo.srcObject) self._showRemotePlaceholder(false);
      } else if (st === 'failed') {
        self.setStatus('Conexão de vídeo falhou. Tentando restabelecer…', 'warning');
        self._restartIce();
      }
    };
  };

  VideoConsultaClient.prototype._restartIce = async function () {
    if (!this.pc || !this.peerConnectionId) return;
    log('ICE restart');
    try {
      await this._createAndSendOffer(true);
    } catch (e) {
      console.error('ICE restart', e);
      // Recria PC por completo
      this._closePeerConnection(false);
      this._ensurePeerConnection();
      this._scheduleNegotiate(300);
    }
  };

  VideoConsultaClient.prototype._createAndSendOffer = async function (iceRestart) {
    if (!this.peerConnectionId || !this.connection) return;
    this._ensurePeerConnection();

    // Polite só oferece se a oferta do impolite não chegou (fallback)
    if (this.polite && !iceRestart) {
      if (this._mediaConnected) return;
      if (this.pc.remoteDescription) return;
      if (this.pc.signalingState !== 'stable') return;
    }

    if (this.makingOffer) {
      log('offer já em andamento — ignorando');
      return;
    }
    if (this.pc.signalingState !== 'stable' && !iceRestart) {
      log('signalingState não stable:', this.pc.signalingState);
      return;
    }

    try {
      this.makingOffer = true;
      var offer = await this.pc.createOffer(iceRestart ? { iceRestart: true } : undefined);
      // Glare pode ter mudado o estado enquanto await
      if (this.pc.signalingState !== 'stable' && !iceRestart) {
        log('abort offer — estado mudou para', this.pc.signalingState);
        return;
      }
      await this.pc.setLocalDescription(offer);
      await this.connection.invoke('SendOffer', this.peerConnectionId, this.pc.localDescription.sdp);
      log('Offer enviada', iceRestart ? '(ICE restart)' : '', 'polite=', this.polite);
      this.setStatus('Negociando vídeo com ' + (this.remoteNameEl ? this.remoteNameEl.textContent : this.remoteNameDefault) + '…', 'info');
    } catch (e) {
      console.error('createOffer', e);
      this.setStatus('Não foi possível iniciar a chamada.', 'danger');
    } finally {
      this.makingOffer = false;
    }
  };

  VideoConsultaClient.prototype.callPeer = async function () {
    if (!this.connection) await this.start();
    await this.connection.invoke('CallPeer');
    this.setStatus('Chamando o outro participante…', 'warning');
    if (this.peerConnectionId) await this._createAndSendOffer(false);
  };

  VideoConsultaClient.prototype.toggleMute = function () {
    if (!this.localStream) return false;
    var track = this.localStream.getAudioTracks()[0];
    if (!track) return false;
    track.enabled = !track.enabled;
    return !track.enabled;
  };

  VideoConsultaClient.prototype.toggleCamera = function () {
    if (!this.localStream) return false;
    var track = this.localStream.getVideoTracks()[0];
    if (!track) return false;
    track.enabled = !track.enabled;
    return !track.enabled;
  };

  VideoConsultaClient.prototype._closePeerConnection = function (stopLocal) {
    if (this._negotiateTimer) {
      clearTimeout(this._negotiateTimer);
      this._negotiateTimer = null;
    }
    if (this._iceRestartTimer) {
      clearTimeout(this._iceRestartTimer);
      this._iceRestartTimer = null;
    }
    this._pendingIce = [];
    this.makingOffer = false;
    this.ignoreOffer = false;
    this._mediaConnected = false;
    if (this.pc) {
      try { this.pc.onicecandidate = null; this.pc.ontrack = null; this.pc.close(); } catch (e) {}
      this.pc = null;
    }
    if (this.remoteVideo) this.remoteVideo.srcObject = null;
    if (stopLocal && this.localStream) {
      this.localStream.getTracks().forEach(function (t) { t.stop(); });
      this.localStream = null;
      if (this.localVideo) this.localVideo.srcObject = null;
    }
  };

  VideoConsultaClient.prototype.hangUp = async function () {
    try {
      if (this.connection) await this.connection.invoke('LeaveRoom');
    } catch (e) {}
    this._closePeerConnection(true);
    if (this.connection) {
      try { await this.connection.stop(); } catch (e) {}
      this.connection = null;
    }
    this._started = false;
    this.peerConnectionId = null;
    this.setStatus('Chamada encerrada.', 'secondary');
    this._setPeerStatus('—');
    this._showRemotePlaceholder(true);
  };

  global.VideoConsultaClient = VideoConsultaClient;
})(window);
