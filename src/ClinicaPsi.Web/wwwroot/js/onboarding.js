(function () {
    'use strict';

    var STEPS = {
        cliente: [
            {
                title: 'Bem-vindo à sua área',
                text: 'Aqui você acompanha consultas, agenda horários e acessa a sala de vídeo quando a psicóloga iniciar a chamada.',
                selector: null
            },
            {
                title: 'Agendar consulta',
                text: 'Use Agendar Consulta para escolher data, horário e formato (presencial ou online).',
                selector: '[data-tour="cliente-agendar"]',
                href: '/Cliente/AgendarConsulta'
            },
            {
                title: 'Minhas consultas e sala',
                text: 'Em Minhas Consultas você vê os próximos atendimentos e entra na sala de vídeo quando estiver disponível. Também recebe aviso na tela se a chamada começar.',
                selector: '[data-tour="cliente-consultas"]',
                href: '/Cliente/MinhasConsultas'
            },
            {
                title: 'Histórico',
                text: 'O Histórico guarda as sessões já realizadas para você consultar quando precisar.',
                selector: '[data-tour="cliente-historico"]',
                href: '/Cliente/Historico'
            },
            {
                title: 'Seu perfil',
                text: 'Em Meu Perfil você atualiza dados de contato. Pronto — explore o sistema no seu ritmo.',
                selector: '[data-tour="cliente-perfil"]',
                href: '/Cliente/MeuPerfil'
            }
        ],
        psicologo: [
            {
                title: 'Área do psicólogo',
                text: 'Este painel resume o dia, a agenda e o acesso rápido às ferramentas clínicas.',
                selector: null
            },
            {
                title: 'Agenda',
                text: 'Em Minha Agenda você organiza o dia e abre a sala de vídeo nas consultas online.',
                selector: '[data-tour="psico-agenda"]',
                href: '/psicologo/agenda'
            },
            {
                title: 'Pacientes',
                text: 'Em Meus Pacientes você cadasta, edita e acompanha a lista de pacientes.',
                selector: '[data-tour="psico-pacientes"]',
                href: '/psicologo/pacientes'
            },
            {
                title: 'Prontuário',
                text: 'O prontuário eletrônico registra evolução e observações das sessões.',
                selector: '[data-tour="psico-prontuario"]',
                href: '/prontuario'
            },
            {
                title: 'Documentos e vídeo',
                text: 'Gere declarações em Documentos. Nas consultas online, a sala de vídeo fica na agenda/consultas — o layout Meet permanece intacto.',
                selector: '[data-tour="psico-documentos"]',
                href: '/psicologo/documentos'
            },
            {
                title: 'Relatórios e perfil',
                text: 'Use Relatórios para visão mensal e Meu Perfil para seus dados profissionais.',
                selector: '[data-tour="psico-relatorios"]',
                href: '/psicologo/relatorios'
            }
        ]
    };

    function qs(sel, root) {
        return (root || document).querySelector(sel);
    }

    function postJson(url) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
            body: '{}'
        }).catch(function () { return null; });
    }

    function init() {
        var root = qs('#onboarding-root');
        if (!root || root.dataset.bootstrapped === '1') return;
        root.dataset.bootstrapped = '1';

        var role = root.dataset.role;
        var steps = STEPS[role];
        if (!steps || !steps.length) return;

        var storageKey = 'clinicapsi.onboarding.' + role;
        try {
            if (localStorage.getItem(storageKey) === 'done') {
                postJson(root.dataset.apiDismiss);
                return;
            }
        } catch (e) { /* ignore */ }

        var idx = 0;
        var activeEl = null;
        var panel = qs('.onboarding-panel', root);
        var spotlight = qs('.onboarding-spotlight', root);
        var titleEl = qs('#onboarding-title', root);
        var textEl = qs('#onboarding-text', root);
        var stepLabel = qs('#onboarding-step-label', root);
        var bar = qs('#onboarding-progress-bar', root);
        var btnPrev = qs('#onboarding-prev', root);
        var btnNext = qs('#onboarding-next', root);
        var btnSkip = qs('#onboarding-skip', root);
        var btnNever = qs('#onboarding-never', root);

        function clearHighlight() {
            if (activeEl) {
                activeEl.classList.remove('tour-target-active');
                activeEl = null;
            }
            spotlight.classList.remove('is-visible');
            spotlight.style.cssText = '';
        }

        function placeSpotlight(el) {
            if (!el) {
                clearHighlight();
                return;
            }
            activeEl = el;
            el.classList.add('tour-target-active');
            var rect = el.getBoundingClientRect();
            var pad = 8;
            spotlight.classList.add('is-visible');
            spotlight.style.top = Math.max(8, rect.top - pad) + 'px';
            spotlight.style.left = Math.max(8, rect.left - pad) + 'px';
            spotlight.style.width = Math.min(window.innerWidth - 16, rect.width + pad * 2) + 'px';
            spotlight.style.height = Math.min(window.innerHeight - 16, rect.height + pad * 2) + 'px';
        }

        function positionPanel(el) {
            panel.style.left = '';
            panel.style.right = '';
            panel.style.top = '';
            panel.style.bottom = '';
            panel.style.transform = '';

            if (window.innerWidth < 640 || !el) {
                panel.style.left = '50%';
                panel.style.bottom = '1.25rem';
                panel.style.transform = 'translateX(-50%)';
                return;
            }

            var rect = el.getBoundingClientRect();
            var panelW = Math.min(440, window.innerWidth - 24);
            var left = Math.min(window.innerWidth - panelW - 12, Math.max(12, rect.left));
            var top = rect.bottom + 14;
            if (top + 220 > window.innerHeight) {
                top = Math.max(12, rect.top - 230);
            }
            panel.style.left = left + 'px';
            panel.style.top = top + 'px';
            panel.style.bottom = 'auto';
            panel.style.transform = 'none';
            panel.style.width = panelW + 'px';
        }

        function render() {
            var step = steps[idx];
            titleEl.textContent = step.title;
            textEl.textContent = step.text;
            stepLabel.textContent = 'Passo ' + (idx + 1) + ' de ' + steps.length;
            bar.style.width = (((idx + 1) / steps.length) * 100) + '%';
            btnPrev.disabled = idx === 0;
            btnNext.textContent = idx === steps.length - 1 ? 'Concluir' : 'Próximo';

            clearHighlight();
            var el = step.selector ? qs(step.selector) : null;
            if (el) {
                try { el.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); } catch (e) { /* ignore */ }
                placeSpotlight(el);
            }
            positionPanel(el);
        }

        function finish(persistUrl) {
            try { localStorage.setItem(storageKey, 'done'); } catch (e) { /* ignore */ }
            clearHighlight();
            root.classList.remove('is-active');
            root.hidden = true;
            if (persistUrl) postJson(persistUrl);
        }

        function go(delta) {
            var next = idx + delta;
            if (next < 0) return;
            if (next >= steps.length) {
                finish(root.dataset.apiComplete);
                return;
            }
            idx = next;
            render();
        }

        btnPrev.addEventListener('click', function () { go(-1); });
        btnNext.addEventListener('click', function () { go(1); });
        btnSkip.addEventListener('click', function () { finish(root.dataset.apiComplete); });
        btnNever.addEventListener('click', function () { finish(root.dataset.apiDismiss); });

        window.addEventListener('resize', function () {
            if (!root.classList.contains('is-active')) return;
            render();
        });

        document.addEventListener('keydown', function (ev) {
            if (!root.classList.contains('is-active')) return;
            if (ev.key === 'Escape') finish(root.dataset.apiComplete);
            if (ev.key === 'ArrowRight') go(1);
            if (ev.key === 'ArrowLeft') go(-1);
        });

        root.hidden = false;
        root.classList.add('is-active');
        render();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Permite reabrir o tour a partir do menu
    window.ClinicaPsiOnboarding = {
        reset: function () {
            fetch('/api/onboarding/reset', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
                body: '{}'
            }).then(function () {
                try {
                    localStorage.removeItem('clinicapsi.onboarding.cliente');
                    localStorage.removeItem('clinicapsi.onboarding.psicologo');
                } catch (e) { /* ignore */ }
                window.location.reload();
            });
        }
    };
})();
