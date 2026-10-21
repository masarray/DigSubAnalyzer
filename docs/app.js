
// Public release discovery. GitHub /releases/latest excludes prereleases and
// drafts. Every anchor already has a working direct stable EXE fallback in HTML,
// so API outages, offline use, rate limits, and disabled JavaScript never send
// a user to an obsolete "portable ZIP" CTA or a CI artifact.
const releaseApi = 'https://api.github.com/repos/masarray/DigSubAnalyzer/releases/latest';
const releaseAssetRoot = 'https://github.com/masarray/DigSubAnalyzer/releases/download/';
const releaseFilename = /^ProcessBusInsight-v\d+\.\d+\.\d+-win-x64-(setup\.exe|portable\.zip)$/;
const checksumFilename = 'SHA256SUMS.txt';

function safeReleaseAsset(asset, tag) {
  if (!asset || typeof asset.name !== 'string' || typeof asset.browser_download_url !== 'string') return false;
  if (!(releaseFilename.test(asset.name) || asset.name === checksumFilename)) return false;
  const prefix = `${releaseAssetRoot}${tag}/`;
  return asset.browser_download_url === prefix + asset.name;
}

function setReleaseLinks(kind, url) {
  document.querySelectorAll(`[data-release-download="${kind}"]`).forEach((anchor) => {
    anchor.href = url;
  });
}

function applyLatestStableRelease(release) {
  if (!release || release.draft || release.prerelease ||
      !/^v\d+\.\d+\.\d+$/.test(release.tag_name) || !Array.isArray(release.assets)) return;

  const tag = release.tag_name;
  const assets = release.assets.filter((asset) => safeReleaseAsset(asset, tag));
  const exe = assets.find((asset) => asset.name === `ProcessBusInsight-${tag}-win-x64-setup.exe`);
  if (!exe) return; // Never direct the primary button to ZIP or a nonexistent asset.
  const portable = assets.find((asset) => asset.name === `ProcessBusInsight-${tag}-win-x64-portable.zip`);
  const checksum = assets.find((asset) => asset.name === checksumFilename);
  const releasePage = `https://github.com/masarray/DigSubAnalyzer/releases/tag/${tag}`;

  setReleaseLinks('installer', exe.browser_download_url);
  if (portable) setReleaseLinks('portable', portable.browser_download_url);
  if (checksum) setReleaseLinks('checksum', checksum.browser_download_url);
  document.querySelectorAll('[data-release-page]').forEach((link) => { link.href = releasePage; });
  document.querySelectorAll('[data-release-version]').forEach((label) => {
    label.textContent = `Stable ${tag}`;
  });

  // Keep structured download metadata coherent for crawlers that execute JS.
  const structured = document.querySelector('script[type="application/ld+json"]');
  if (structured) {
    try {
      const data = JSON.parse(structured.textContent);
      if (data['@type'] === 'SoftwareApplication') {
        data.softwareVersion = tag.slice(1);
        data.downloadUrl = exe.browser_download_url;
        structured.textContent = JSON.stringify(data);
      }
    } catch { /* Noncritical metadata; keep the static stable fallback. */ }
  }
}

if (document.querySelector('[data-release-download="installer"]')) {
  fetch(releaseApi, {
    headers: { Accept: 'application/vnd.github+json' },
    cache: 'no-store'
  }).then((response) => {
    if (!response.ok) throw new Error('GitHub latest release not accessible');
    return response.json();
  }).then(applyLatestStableRelease).catch(() => {
    // HTML already points directly to verified v1.4.2 EXE.
  });
}

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
