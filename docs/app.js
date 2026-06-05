const header = document.querySelector("[data-header]");
const progress = document.querySelector(".scroll-progress");
const revealItems = document.querySelectorAll(".reveal");
const depthItems = document.querySelectorAll("[data-depth]");
const magneticItems = document.querySelectorAll(".magnetic");
const tiltCards = document.querySelectorAll(".tilt-card");
const navLinks = document.querySelectorAll(".nav-links a");
const sections = [...document.querySelectorAll("main section[id]")];

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

function updateChrome() {
  const scrollTop = window.scrollY || document.documentElement.scrollTop;
  const docHeight = document.documentElement.scrollHeight - window.innerHeight;
  const ratio = docHeight > 0 ? scrollTop / docHeight : 0;

  if (progress) {
    progress.style.width = `${Math.min(100, Math.max(0, ratio * 100))}%`;
  }

  if (header) {
    header.classList.toggle("is-compact", scrollTop > 80);
  }

  if (!reduceMotion) {
    depthItems.forEach((item) => {
      const depth = Number(item.dataset.depth || "0");
      const rect = item.getBoundingClientRect();
      const center = rect.top + rect.height / 2 - window.innerHeight / 2;
      item.style.transform = `translate3d(0, ${center * depth * -0.12}px, 0)`;
    });
  }

  let activeId = sections[0]?.id;
  sections.forEach((section) => {
    const rect = section.getBoundingClientRect();
    if (rect.top < window.innerHeight * 0.42) {
      activeId = section.id;
    }
  });

  navLinks.forEach((link) => {
    const id = link.getAttribute("href")?.replace("#", "");
    link.classList.toggle("is-active", id === activeId);
  });
}

if ("IntersectionObserver" in window && !reduceMotion) {
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      }
    });
  }, {
    threshold: 0.16,
    rootMargin: "0px 0px -8% 0px"
  });

  revealItems.forEach((item) => observer.observe(item));
} else {
  revealItems.forEach((item) => item.classList.add("is-visible"));
}

magneticItems.forEach((item) => {
  item.addEventListener("mousemove", (event) => {
    if (reduceMotion) return;
    const rect = item.getBoundingClientRect();
    const x = event.clientX - rect.left - rect.width / 2;
    const y = event.clientY - rect.top - rect.height / 2;
    item.style.transform = `translate(${x * 0.12}px, ${y * 0.16}px)`;
  });

  item.addEventListener("mouseleave", () => {
    item.style.transform = "";
  });
});

tiltCards.forEach((card) => {
  card.addEventListener("mousemove", (event) => {
    if (reduceMotion) return;
    const rect = card.getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - 0.5;
    const y = (event.clientY - rect.top) / rect.height - 0.5;
    card.style.transform = `perspective(900px) rotateX(${y * -7}deg) rotateY(${x * 8}deg) translateY(-4px)`;
  });

  card.addEventListener("mouseleave", () => {
    card.style.transform = "";
  });
});

document.querySelectorAll(".audience-item").forEach((item) => {
  item.addEventListener("click", () => {
    document.querySelectorAll(".audience-item").forEach((button) => button.classList.remove("is-selected"));
    item.classList.add("is-selected");
  });
});

window.addEventListener("scroll", updateChrome, { passive: true });
window.addEventListener("resize", updateChrome);
updateChrome();
