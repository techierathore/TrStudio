/* TrStudio theme persistence (REQ-UI-005) — shared by TrStudio and TrStudioAdmin.
   Loaded synchronously in <head> AFTER the stylesheets so the persisted theme is
   applied before first paint (no flash). Dark is the default; both hosts also
   render <html class="dark"> statically so even script-less loads start dark. */
(function () {
  var KEY = "trstudio-theme";

  function readStored() {
    try {
      return window.localStorage.getItem(KEY);
    } catch (e) {
      return null;
    }
  }

  function apply(theme) {
    document.documentElement.classList.toggle("dark", theme !== "light");
  }

  // Dark unless the user explicitly persisted light.
  apply(readStored() === "light" ? "light" : "dark");

  window.trStudioTheme = {
    /** Returns true when the dark theme is active. */
    isDark: function () {
      return document.documentElement.classList.contains("dark");
    },
    /** Flips the theme, persists the choice, and returns true when now dark. */
    toggle: function () {
      var next = document.documentElement.classList.contains("dark") ? "light" : "dark";
      try {
        window.localStorage.setItem(KEY, next);
      } catch (e) { /* private mode — theme still flips for this page */ }
      apply(next);
      return next === "dark";
    }
  };
})();
