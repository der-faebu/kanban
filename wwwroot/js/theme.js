// Companion to the blocking inline script in App.razor's <head>, which sets
// data-theme synchronously before first paint using the same storage key and
// fallback. These functions are for the interactive toggle, called via IJSRuntime.
window.knbGetTheme = function () {
    var stored = localStorage.getItem('knb-theme');
    return stored || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
};

window.knbSetTheme = function (theme) {
    localStorage.setItem('knb-theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
};
