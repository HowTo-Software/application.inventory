// Download helper for Blazor - triggers file download from byte array
window.downloadFileFromBytes = function (fileName, contentType, bytes) {
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Focus trap for mobile sidebar navigation
window.NavMenuFocusTrap = {
    _navElement: null,
    _dotNetRef: null,
    _onKeyDown: null,

    init: function (navElement, dotNetRef) {
        this._navElement = navElement;
        this._dotNetRef = dotNetRef;
    },

    activate: function (navElement) {
        this._navElement = navElement;
        var self = this;

        this._onKeyDown = function (e) {
            if (e.key === 'Escape') {
                e.preventDefault();
                if (self._dotNetRef) {
                    self._dotNetRef.invokeMethodAsync('CloseSidebar');
                }
                return;
            }

            if (e.key !== 'Tab') return;

            var focusable = navElement.querySelectorAll(
                'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );
            if (focusable.length === 0) return;

            var first = focusable[0];
            var last = focusable[focusable.length - 1];

            if (e.shiftKey) {
                if (document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                }
            } else {
                if (document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            }
        };

        document.addEventListener('keydown', this._onKeyDown);

        var firstLink = navElement.querySelector('a[href]');
        if (firstLink) firstLink.focus();
    },

    deactivate: function () {
        if (this._onKeyDown) {
            document.removeEventListener('keydown', this._onKeyDown);
            this._onKeyDown = null;
        }
    }
};

// Focus trap for modal dialogs (session expiration, etc.)
window.ModalFocusTrap = {
    _element: null,
    _onKeyDown: null,
    _previouslyFocused: null,

    activate: function (element) {
        this.deactivate();

        this._element = element;
        this._previouslyFocused = document.activeElement;

        var self = this;
        this._onKeyDown = function (e) {
            if (e.key !== 'Tab') return;

            var focusable = element.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );
            if (focusable.length === 0) {
                e.preventDefault();
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];

            if (e.shiftKey) {
                if (document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                }
            } else {
                if (document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            }
        };

        document.addEventListener('keydown', this._onKeyDown);

        // Focus the first focusable element inside the modal
        var focusable = element.querySelectorAll(
            'a[href], button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        if (focusable.length > 0) {
            focusable[0].focus();
        }
    },

    deactivate: function () {
        if (this._onKeyDown) {
            document.removeEventListener('keydown', this._onKeyDown);
            this._onKeyDown = null;
        }
        if (this._previouslyFocused && this._previouslyFocused.focus) {
            this._previouslyFocused.focus();
            this._previouslyFocused = null;
        }
        this._element = null;
    }
};

// Clipboard helper for Blazor (with fallback for non-HTTPS)
window.copyToClipboard = async function (text) {
    if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
    } else {
        // Fallback for non-secure contexts (HTTP)
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-9999px';
        document.body.appendChild(textArea);
        textArea.select();
        document.execCommand('copy');
        document.body.removeChild(textArea);
    }
};
