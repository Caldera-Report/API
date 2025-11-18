window.theme = {
  set: function (mode) {
    const root = document.documentElement;
    root.classList.remove("force-light", "force-dark");
    if (mode === "light") {
      root.classList.add("force-light");
    } else if (mode === "dark") {
      root.classList.add("force-dark");
    }
    localStorage.setItem("theme-mode", mode);
  },
  get: function () {
    return localStorage.getItem("theme-mode") || "auto";
  },
  init: function () {
    const saved = this.get();
    this.set(saved);
  },
  cycle: function () {
    const order = ["auto", "dark", "light"];
    const current = this.get();
    const next = order[(order.indexOf(current) + 1) % order.length];
    this.set(next);
    return next;
  },
};
window.theme.init();
