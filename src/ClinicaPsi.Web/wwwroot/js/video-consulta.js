/**
 * Videochamada 1:1 WebRTC + SignalR (sem conta em provedor externo).
 * Exibe displayName da psicóloga e do paciente na UI.
 */
(function (global) {
  'use strict';

  const ICE_SERVERS = [
    { urls: 'stun:stun.l.google.com:19302' },
    { urls: 'stun:stun1.l.google.com:19302' }
  ];

  function VideoConsultaClient(options) {
    this.consultaId = options.consultaId;
    this.roomName = options.roomName;
    this.displayName = options.displayName;
    this.role = options.role;
    this.remoteNameDefault = options.remoteNameDefault || 'Participante';
    this.hubUrl = options.hubUrl || '/hubs/video-consulta';

    this.localVideo = options.localVideo;
    this.remoteVideo = options.remoteVideo;
    this.statusEl = options.statusEl;
    this.localNameEl = options.localNameEl;
    this.remoteNameEl = options.remoteNameEl;
    this.peerStatusEl = options.peerStatusEl;

    this.connection = null;
    this.pc = null;
    this.localStream = null;
    this.peerConnectionId = null;
    this.makingOffer = false;
    this.polite = options.role !== 'Psicologo'; // paciente é "polite" no glare
    this._started = false;
  }

  VideoConsultaClient.prototype.setStatus = function (text, kind) {
    if (!this.statusEl) return;
    this.statusEl.textContent = text;
    this.statusEl.className = 'video-status alert mb-3 alert-' + (kind || 'info');
  };

  VideoConsultaClient.prototype.setRemoteName = function (name) {
    if (this.remoteNameEl) this.remoteNameEl.textContent = name || this.remoteNameDefault;
  };

  VideoConsultaClient.prototype.start = async function () {
    if (this._started) return;
    this._started = true;

    if (this.localNameEl) this.localNameEl.textContent = this.displayName;
    this.setRemoteName(this.remoteNameDefault);
    this.setStatus('Solicitando câmera e microfone…', 'info');

    try {
      this.localStream = await navigator.mediaDevices.getUserMedia({
        audio: true,
        video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } }
      });
      if (this.localVideo) {
        this.localVideo.srcObject = this.localStream;
        this.localVideo.muted = true;
        await this.localVideo.play().catch(function () {});
      }
    } catch (err) {
      console.error(err);
      this.setStatus('Não foi possível acessar câmera/microfone. Verifique as permissões do navegador.', 'danger');
      this._started = false;
      throw err;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();

    this._wireHub();

    this.setStatus('Conectando à sala…', 'info');
    await this.connection.start();
    await this.connection.invoke('JoinRoom', this.consultaId, this.roomName, this.displayName, this.role);
  };

  VideoConsultaClient.prototype._wireHub = function () {
    var self = this;

    this.connection.on('RoomJoined', function (payload) {
      var peers = (payload && payload.peers) || [];
      if (peers.length === 0) {
        self.setStatus('Na sala. Aguardando o outro participante…', 'info');
        if (self.peerStatusEl) self.peerStatusEl.textContent = 'Aguardando…';
        return;
      }
      var peer = peers[0];
      self.peerConnectionId = peer.connectionId;
      self.setRemoteName(peer.displayName);
      if (self.peerStatusEl) self.peerStatusEl.textContent = 'Online';
      self.setStatus(peer.displayName + ' já está na sala. Conectando vídeo…', 'success');
      self._ensurePeerConnection();
      // Psicólogo/Admin inicia a oferta; paciente só responde
      if (self.role === 'Psicologo' || self.role === 'Admin') {
        self._createAndSendOffer();
      }
    });

    this.connection.on('PeerJoined', function (peer) {
      self.peerConnectionId = peer.connectionId;
      self.setRemoteName(peer.displayName);
      if (self.peerStatusEl) self.peerStatusEl.textContent = 'Online';
      self.setStatus(peer.displayName + ' entrou na sala.', 'success');
      self._ensurePeerConnection();
      if (self.role === 'Psicologo' || self.role === 'Admin') {
        self._createAndSendOffer();
      }
    });

    this.connection.on('IncomingCall', function (peer) {
      self.peerConnectionId = peer.connectionId;
      self.setRemoteName(peer.displayName);
      self.setStatus(peer.displayName + ' está chamando…', 'warning');
      self._ensurePeerConnection();
    });

    this.connection.on('PeerLeft', function (peer) {
      if (self.peerConnectionId && peer.connectionId !== self.peerConnectionId) return;
      self.setStatus((peer.displayName || 'Participante') + ' saiu da sala.', 'warning');
      if (self.peerStatusEl) self.peerStatusEl.textContent = 'Desconectado';
      self._closePeerConnection(false);
      self.peerConnectionId = null;
    });

    this.connection.on('ReceiveOffer', async function (msg) {
      try {
        self.peerConnectionId = msg.fromConnectionId;
        if (msg.displayName) self.setRemoteName(msg.displayName);
        self._ensurePeerConnection();
        var offerCollision = self.makingOffer || self.pc.signalingState !== 'stable';
        if (offerCollision && !self.polite) return;
        await self.pc.setRemoteDescription({ type: 'offer', sdp: msg.sdp });
        var answer = await self.pc.createAnswer();
        await self.pc.setLocalDescription(answer);
        await self.connection.invoke('SendAnswer', msg.fromConnectionId, answer.sdp);
        self.setStatus('Chamada em andamento com ' + (msg.displayName || self.remoteNameDefault), 'success');
      } catch (e) {
        console.error('ReceiveOffer', e);
        self.setStatus('Falha ao aceitar a chamada.', 'danger');
      }
    });

    this.connection.on('ReceiveAnswer', async function (msg) {
      try {
        if (!self.pc) return;
        await self.pc.setRemoteDescription({ type: 'answer', sdp: msg.sdp });
        self.setStatus('Conectado com ' + (msg.displayName || self.remoteNameDefault), 'success');
      } catch (e) {
        console.error('ReceiveAnswer', e);
      }
    });

    this.connection.on('ReceiveIceCandidate', async function (msg) {
      try {
        if (!self.pc || !msg.candidate) return;
        var cand = typeof msg.candidate === 'string' ? JSON.parse(msg.candidate) : msg.candidate;
        if (cand) await self.pc.addIceCandidate(cand);
      } catch (e) {
        console.warn('ICE candidate', e);
      }
    });

    this.connection.onreconnecting(function () {
      self.setStatus('Reconectando sinalização…', 'warning');
    });

    this.connection.onreconnected(async function () {
      self.setStatus('Sinalização reconectada. Reentrando na sala…', 'info');
      try {
        await self.connection.invoke('JoinRoom', self.consultaId, self.roomName, self.displayName, self.role);
      } catch (e) {
        console.error(e);
      }
    });
  };

  VideoConsultaClient.prototype._ensurePeerConnection = function () {
    if (this.pc) return;
    var self = this;
    this.pc = new RTCPeerConnection({ iceServers: ICE_SERVERS });

    if (this.localStream) {
      this.localStream.getTracks().forEach(function (track) {
        self.pc.addTrack(track, self.localStream);
      });
    }

    this.pc.ontrack = function (ev) {
      if (self.remoteVideo) {
        self.remoteVideo.srcObject = ev.streams[0];
        self.remoteVideo.play().catch(function () {});
      }
      self.setStatus('Vídeo conectado.', 'success');
      if (self.peerStatusEl) self.peerStatusEl.textContent = 'Em chamada';
    };

    this.pc.onicecandidate = function (ev) {
      if (!ev.candidate || !self.peerConnectionId || !self.connection) return;
      self.connection.invoke('SendIceCandidate', self.peerConnectionId, JSON.stringify(ev.candidate.toJSON()))
        .catch(function (e) { console.warn(e); });
    };

    this.pc.onconnectionstatechange = function () {
      var st = self.pc && self.pc.connectionState;
      if (st === 'connected') {
        self.setStatus('Conectado com ' + (self.remoteNameEl ? self.remoteNameEl.textContent : self.remoteNameDefault), 'success');
      } else if (st === 'failed' || st === 'disconnected') {
        self.setStatus('Conexão de vídeo interrompida. Tentando restabelecer…', 'warning');
      }
    };
  };

  VideoConsultaClient.prototype._createAndSendOffer = async function () {
    if (!this.peerConnectionId) return;
    this._ensurePeerConnection();
    try {
      this.makingOffer = true;
      var offer = await this.pc.createOffer();
      await this.pc.setLocalDescription(offer);
      await this.connection.invoke('SendOffer', this.peerConnectionId, offer.sdp);
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
    if (this.peerConnectionId) await this._createAndSendOffer();
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
    if (this.pc) {
      try { this.pc.close(); } catch (e) {}
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
    if (this.peerStatusEl) this.peerStatusEl.textContent = '—';
  };

  global.VideoConsultaClient = VideoConsultaClient;
})(window);
