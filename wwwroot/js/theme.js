// Companion to the blocking inline script in App.razor's <head> (which also installs
// the self-healing MutationObserver for data-theme). These two are for interactive
// components' IJSRuntime calls: reading the current preference, and persisting +
// applying a new one when the user actively toggles.
window.knbGetTheme = function () {
    var stored = localStorage.getItem('knb-theme');
    return stored || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
};

window.knbSetTheme = function (theme) {
    localStorage.setItem('knb-theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
};
