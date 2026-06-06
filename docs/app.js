const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const header = document.querySelector('[data-header]');
const progressBar = document.querySelector('.scroll-progress');
const revealItems = document.querySelectorAll('[data-reveal]');
const nav = document.querySelector('[data-segmented-nav]');
const navIndicator = nav?.querySelector('.nav-indicator');
const navLinks = nav ? [...nav.querySelectorAll('a')] : [];
const sections = [...document.querySelectorAll('main section[id]')];
const tiltCards = document.querySelectorAll('.tilt-card');

revealItems.forEach((item) => {
  const delay = item.dataset.delay || '0';
  item.style.setProperty('--reveal-delay', `${delay}ms`);
});

function moveIndicatorTo(link) {
  if (!nav || !navIndicator || !link) return;
  const navRect = nav.getBoundingClientRect();
  const linkRect = link.getBoundingClientRect();
  const x = linkRect.left - navRect.left;
  navIndicator.style.width = `${linkRect.width}px`;
  navIndicator.style.transform = `translateX(${x - 5}px)`;
}

function getActiveSectionId() {
  let activeId = sections[0]?.id;
  sections.forEach((section) => {
    const rect = section.getBoundingClientRect();
    if (rect.top <= window.innerHeight * 0.34) {
      activeId = section.id;
    }
  });
  return activeId;
}

function updateScrollChrome() {
  const scrollTop = window.scrollY || document.documentElement.scrollTop;
  const docHeight = document.documentElement.scrollHeight - window.innerHeight;
  const ratio = docHeight > 0 ? scrollTop / docHeight : 0;

  if (progressBar) {
    progressBar.style.width = `${Math.min(100, Math.max(0, ratio * 100))}%`;
  }

  if (header) {
    header.classList.toggle('is-compact', scrollTop > 44);
  }

  const activeId = getActiveSectionId();
  let activeLink = navLinks[0];
  navLinks.forEach((link) => {
    const target = link.getAttribute('href')?.replace('#', '');
    const isActive = target === activeId;
    link.classList.toggle('is-active', isActive);
    if (isActive) activeLink = link;
  });
  moveIndicatorTo(activeLink);
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
    rootMargin: '0px 0px -7% 0px'
  });

  revealItems.forEach((item) => observer.observe(item));
} else {
  revealItems.forEach((item) => item.classList.add('is-visible'));
}

navLinks.forEach((link) => {
  link.addEventListener('mouseenter', () => moveIndicatorTo(link));
  link.addEventListener('focus', () => moveIndicatorTo(link));
});

nav?.addEventListener('mouseleave', updateScrollChrome);

function attachTilt(card) {
  card.addEventListener('mousemove', (event) => {
    if (reduceMotion || window.innerWidth < 981) return;
    const rect = card.getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - 0.5;
    const y = (event.clientY - rect.top) / rect.height - 0.5;
    card.style.transform = `perspective(1100px) rotateX(${y * -3.8}deg) rotateY(${x * 4.6}deg) translateY(-2px)`;
  });

  card.addEventListener('mouseleave', () => {
    card.style.transform = '';
  });
}

tiltCards.forEach(attachTilt);

window.addEventListener('scroll', updateScrollChrome, { passive: true });
window.addEventListener('resize', updateScrollChrome);
window.addEventListener('load', updateScrollChrome);
updateScrollChrome();
