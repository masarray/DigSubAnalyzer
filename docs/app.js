const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const header = document.querySelector('[data-header]');
const progressBar = document.querySelector('.scroll-progress');
const revealItems = document.querySelectorAll('[data-reveal]');
const navLinks = document.querySelectorAll('.segmented-nav a');
const navIndicator = document.querySelector('.nav-indicator');
const sections = [...document.querySelectorAll('main section[id]')];
const tiltCards = document.querySelectorAll('.tilt-card');
const previewTargets = document.querySelectorAll('[data-preview-src]');
const previewOverlay = document.querySelector('[data-preview-overlay]');
const previewImage = document.querySelector('[data-preview-image]');
const previewCaption = document.querySelector('[data-preview-caption]');

revealItems.forEach((item) => {
  item.style.setProperty('--reveal-delay', `${item.dataset.delay || '0'}ms`);
});

function moveNavIndicator(activeLink) {
  if (!navIndicator || !activeLink) return;
  const nav = activeLink.closest('.segmented-nav');
  const navRect = nav.getBoundingClientRect();
  const linkRect = activeLink.getBoundingClientRect();
  navIndicator.style.width = `${linkRect.width}px`;
  navIndicator.style.transform = `translateX(${linkRect.left - navRect.left - 5}px)`;
}

function updateScrollChrome() {
  const scrollTop = window.scrollY || document.documentElement.scrollTop;
  const docHeight = document.documentElement.scrollHeight - window.innerHeight;
  const ratio = docHeight > 0 ? scrollTop / docHeight : 0;

  if (progressBar) {
    progressBar.style.width = `${Math.min(100, Math.max(0, ratio * 100))}%`;
  }

  if (header) {
    header.classList.toggle('is-compact', scrollTop > 40);
  }

  let activeId = sections[0]?.id || 'download';
  sections.forEach((section) => {
    const rect = section.getBoundingClientRect();
    if (rect.top <= window.innerHeight * 0.32) {
      activeId = section.id;
    }
  });

  let activeLink = null;
  navLinks.forEach((link) => {
    const target = link.getAttribute('href')?.replace('#', '');
    const active = target === activeId;
    link.classList.toggle('is-active', active);
    if (active) activeLink = link;
  });
  moveNavIndicator(activeLink || navLinks[0]);
}

if ('IntersectionObserver' in window && !reduceMotion) {
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      }
    });
  }, {
    threshold: 0.14,
    rootMargin: '0px 0px -8% 0px'
  });

  revealItems.forEach((item) => observer.observe(item));
} else {
  revealItems.forEach((item) => item.classList.add('is-visible'));
}

tiltCards.forEach((card) => {
  card.addEventListener('mousemove', (event) => {
    if (reduceMotion || window.innerWidth < 981 || document.body.classList.contains('preview-open')) return;
    const rect = card.getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - 0.5;
    const y = (event.clientY - rect.top) / rect.height - 0.5;
    card.style.transform = `perspective(1200px) rotateX(${y * -3.6}deg) rotateY(${x * 4.8}deg) translateY(-2px)`;
  });

  card.addEventListener('mouseleave', () => {
    card.style.transform = '';
  });
});

function openPreview(target) {
  if (!previewOverlay || !previewImage) return;
  const src = target.dataset.previewSrc;
  const title = target.dataset.previewTitle || target.querySelector('img')?.alt || 'Screenshot preview';
  previewImage.src = src;
  previewImage.alt = title;
  if (previewCaption) previewCaption.textContent = title;
  previewOverlay.classList.add('is-open');
  previewOverlay.setAttribute('aria-hidden', 'false');
  document.body.classList.add('preview-open');
}

function closePreview() {
  if (!previewOverlay || !previewImage) return;
  previewOverlay.classList.remove('is-open');
  previewOverlay.setAttribute('aria-hidden', 'true');
  document.body.classList.remove('preview-open');
  window.setTimeout(() => {
    if (!previewOverlay.classList.contains('is-open')) previewImage.removeAttribute('src');
  }, 220);
}

previewTargets.forEach((target) => {
  target.addEventListener('click', () => openPreview(target));
  target.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      openPreview(target);
    }
  });
});

if (previewOverlay) {
  previewOverlay.addEventListener('click', closePreview);
}

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') closePreview();
});

navLinks.forEach((link) => {
  link.addEventListener('mouseenter', () => moveNavIndicator(link));
  link.addEventListener('focus', () => moveNavIndicator(link));
});
document.querySelector('[data-segmented-nav]')?.addEventListener('mouseleave', updateScrollChrome);

window.addEventListener('scroll', updateScrollChrome, { passive: true });
window.addEventListener('resize', updateScrollChrome);
updateScrollChrome();
