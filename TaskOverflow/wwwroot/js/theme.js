// Theme management
(function () {
    // Get theme from localStorage or default to light
    const getTheme = () => {
        const savedTheme = localStorage.getItem('theme');
        return savedTheme || 'light';
    };

    // Set theme on document
    const setTheme = (theme) => {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);

        // Update toggle button icon
        const toggleIcon = document.querySelector('.theme-toggle i');
        if (toggleIcon) {
            toggleIcon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon';
        }
    };

    // Toggle theme
    const toggleTheme = () => {
        const currentTheme = getTheme();
        const newTheme = currentTheme === 'light' ? 'dark' : 'light';

        // Add loading class to body
        document.body.classList.add('theme-transition');

        // Set new theme after a tiny delay
        setTimeout(() => {
            setTheme(newTheme);

            // Remove loading class after transition
            setTimeout(() => {
                document.body.classList.remove('theme-transition');
            }, 300);
        }, 50);
    };

    // Initialize theme
    setTheme(getTheme());

    // Make toggle function global
    window.toggleTheme = toggleTheme;
})();

// Add smooth page transitions
document.addEventListener('DOMContentLoaded', () => {
    // Add fade-in class to main content
    document.querySelector('main')?.classList.add('fade-in');

    // Add slide-up class to cards
    document.querySelectorAll('.card').forEach((card, index) => {
        setTimeout(() => {
            card.classList.add('slide-up');
        }, index * 100);
    });

    // Add tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});

// Toast notifications
function showToast(message, type = 'success') {
    const toastContainer = document.getElementById('toast-container') || createToastContainer();

    const toast = document.createElement('div');
    toast.className = `toast align-items-center text-white bg-${type} border-0 slide-up`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');

    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
    `;

    toastContainer.appendChild(toast);
    const bsToast = new bootstrap.Toast(toast);
    bsToast.show();

    // Remove toast after it's hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
    container.style.zIndex = '11';
    document.body.appendChild(container);
    return container;
}