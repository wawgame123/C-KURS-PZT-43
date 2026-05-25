const themeToggle =
    document.getElementById('themeToggle');

function applyTheme(theme) {
    if (theme === 'dark') {
        document.body.classList.add('dark-theme');
    }
    else {
        document.body.classList.remove('dark-theme');
    }
}

const savedTheme =
    localStorage.getItem('theme');

if (savedTheme) {
    applyTheme(savedTheme);
}
else {
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
        applyTheme('dark');
    }
}

if (themeToggle) {
    themeToggle.addEventListener('click', () => {
        const isDark =
            document.body.classList.contains('dark-theme');

        const newTheme =
            isDark ? 'light' : 'dark';

        localStorage.setItem('theme', newTheme);

        applyTheme(newTheme);
    });
}