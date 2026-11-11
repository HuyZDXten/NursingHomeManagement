"use strict";

document.querySelectorAll("form[data-confirm]").forEach(function (form) {
    form.addEventListener("submit", function (event) {
        if (!window.confirm(form.dataset.confirm)) {
            event.preventDefault();
        }
    });
});

document.querySelectorAll(".sidebar nav a").forEach(function (link) {
    if (link.pathname === window.location.pathname ||
        (window.location.pathname === "/" && link.pathname === "/Dashboard")) {
        link.classList.add("active");
        link.setAttribute("aria-current", "page");
    }
});
