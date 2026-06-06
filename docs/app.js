const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const header = document.querySelector('[data-header]');
const progressBar = document.querySelector('.scroll-progress');
const revealItems = document.querySelectorAll('[data-reveal]');
const navLinks = document.querySelectorAll('.nav-links a');
const sections = [...document.querySelectorAll('main section[id]')];
const tiltCards = document.querySelectorAll('.tilt-card');

revealItems.forEach((item) => {
  const delay = item.dataset.delay || '0';
  item.style.setProperty('--reveal-delay', `${delay}ms`);
});

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

  let activeId = sections[0]?.id;
  sections.forEach((section) => {
    const rect = section.getBoundingClientRect();
    if (rect.top <= window.innerHeight * 0.3) {
      activeId = section.id;
    }
  });

  navLinks.forEach((link) => {
    const target = link.getAttribute('href')?.replace('#', '');
    link.classList.toggle('is-active', target === activeId);
  });
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
    if (reduceMotion || window.innerWidth < 981) return;
    const rect = card.getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - 0.5;
    const y = (event.clientY - rect.top) / rect.height - 0.5;
    card.style.transform = `perspective(1200px) rotateX(${y * -4.5}deg) rotateY(${x * 5.5}deg) translateY(-2px)`;
  });

  card.addEventListener('mouseleave', () => {
    card.style.transform = '';
  });
});

window.addEventListener('scroll', updateScrollChrome, { passive: true });
window.addEventListener('resize', updateScrollChrome);
updateScrollChrome();
