/**
 * Notificação global de videochamada para a área do Cliente.
 * SignalR (ChamadaRecebida) + polling /api/notificacoes/chamadas.
 * Atualiza APENAS o banner — nunca recarrega a página nem mexe em modais.
 */
(function (global) {
  'use strict';

  var _started = false;
  var _pollTimer = null;
  var _lastConsultaId = null;

  function ensureBanner() {
    var el = document.getElementById('cliente-chamada-banner');
    if (el) return el;

    el = document.createElement('div');
    el.id = 'cliente-chamada-banner';
    el.className = 'cliente-chamada-banner';
    el.setAttribute('role', 'alert');
    el.hidden = true;
    el.innerHTML =
      '<div class="cliente-chamada-banner__inner">' +
        '<div class="cliente-chamada-banner__text">' +
          '<i class="bi bi-telephone-inbound-fill me-2"></i>' +
          '<strong class="cliente-chamada-banner__title">Chamada em andamento</strong>' +
          '<span class="cliente-chamada-banner__msg"></span>' +
        '</div>' +
        '<div class="cliente-chamada-banner__actions">' +
          '<a class="btn btn-light btn-sm fw-semibold cliente-chamada-banner__enter" href="#">' +
            '<i class="bi bi-camera-video-fill me-1"></i>Entrar na chamada' +
          '</a>' +
          '<button type="button" class="btn btn-outline-light btn-sm cliente-chamada-banner__dismiss" title="Dispensar">' +
            '<i class="bi bi-x-lg"></i>' +
          '</button>' +
        '</div>' +
      '</div>';
    document.body.appendChild(el);

    el.querySelector('.cliente-chamada-banner__dismiss').addEventListener('click', function () {
      el.hidden = true;
      el.dataset.dismissedId = el.dataset.consultaId || '';
    });

    return el;
  }

  function showChamada(chamada) {
    if (!chamada || !chamada.consultaId) return;
    var banner = ensureBanner();
    if (banner.dataset.dismissedId === String(chamada.consultaId)) return;

    // Não mostrar na própria sala de vídeo
    if (/\/consulta\/\d+\/video/i.test(window.location.pathname)) {
      banner.hidden = true;
      return;
    }

    var sameCall = _lastConsultaId === String(chamada.consultaId) && !banner.hidden;
    banner.dataset.consultaId = String(chamada.consultaId);
    _lastConsultaId = String(chamada.consultaId);

    var nome = chamada.psicologoNome || 'seu(sua) psicólogo(a)';
    banner.querySelector('.cliente-chamada-banner__msg').textContent =
      ' — ' + nome + ' está chamando você para a consulta online.';
    var link = banner.querySelector('.cliente-chamada-banner__enter');
    link.href = chamada.videoUrl || ('/consulta/' + chamada.consultaId + '/video');
    banner.hidden = false;

    // Animação só na primeira exibição desta chamada (evita flicker a cada poll)
    if (!sameCall && !banner._pulseDone) {
      banner._pulseDone = true;
      try {
        banner.animate(
          [{ transform: 'translateY(-8px)', opacity: 0.85 }, { transform: 'translateY(0)', opacity: 1 }],
          { duration: 400, easing: 'ease-out' }
        );
      } catch (e) { /* ignore */ }
    }
  }

  function hideIfEmpty(list) {
    var banner = document.getElementById('cliente-chamada-banner');
    if (!banner) return;
    if (!list || !list.length) {
      banner.hidden = true;
      _lastConsultaId = null;
      banner._pulseDone = false;
    }
  }

  async function pollChamadas() {
    // Não interferir enquanto modal Bootstrap estiver aberto
    if (document.body.classList.contains('modal-open')) return;

    try {
      var res = await fetch('/api/notificacoes/chamadas', {
        credentials: 'same-origin',
        headers: { 'Accept': 'application/json' }
      });
      if (!res.ok) return;
      var data = await res.json();
      if (Array.isArray(data) && data.length > 0) {
        showChamada(data[0]);
      } else {
        hideIfEmpty(data);
      }
    } catch (e) {
      console.warn('Polling chamadas:', e);
    }
  }

  function startSignalR() {
    if (typeof signalR === 'undefined') {
      console.warn('SignalR não carregado; usando só polling.');
      return null;
    }

    var connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/video-consulta')
      .withAutomaticReconnect()
      .build();

    connection.on('ChamadaRecebida', function (payload) {
      showChamada(payload);
    });

    connection.start()
      .then(function () { return connection.invoke('SubscribePacienteNotificacoes'); })
      .catch(function (err) { console.warn('SignalR notificações:', err); });

    connection.onreconnected(function () {
      connection.invoke('SubscribePacienteNotificacoes').catch(function () {});
    });

    return connection;
  }

  function initClienteChamadaNotifier(options) {
    if (_started) return;
    _started = true;

    options = options || {};
    var intervalMs = options.pollIntervalMs || 4000;

    ensureBanner();
    startSignalR();
    pollChamadas();
    _pollTimer = setInterval(pollChamadas, intervalMs);
  }

  global.initClienteChamadaNotifier = initClienteChamadaNotifier;
})(window);
