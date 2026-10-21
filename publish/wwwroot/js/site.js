// Souqna Platform Client JavaScript (ES6+)

// 1. Toast Notification Helper
function showToast(message, type = 'success') {
  let toastContainer = document.getElementById('toast-container');
  if (!toastContainer) {
    toastContainer = document.createElement('div');
    toastContainer.id = 'toast-container';
    toastContainer.className = 'toast-container position-fixed bottom-0 start-0 p-3';
    toastContainer.style.zIndex = '9999';
    document.body.appendChild(toastContainer);
  }

  const bgClass = type === 'success' ? 'bg-success text-white' : type === 'danger' ? 'bg-danger text-white' : 'bg-primary text-white';
  const icon = type === 'success' ? 'bi-check-circle' : type === 'danger' ? 'bi-exclamation-triangle' : 'bi-info-circle';

  const toastId = 'toast-' + Date.now();
  const html = `
    <div id="${toastId}" class="toast align-items-center ${bgClass} border-0 shadow-lg" role="alert" aria-live="assertive" aria-atomic="true">
      <div class="d-flex">
        <div class="toast-body d-flex align-items-center gap-2">
          <i class="bi ${icon} fs-5"></i>
          <div>${message}</div>
        </div>
        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
      </div>
    </div>
  `;

  toastContainer.insertAdjacentHTML('beforeend', html);
  const element = document.getElementById(toastId);
  const toast = new bootstrap.Toast(element, { delay: 4000 });
  toast.show();
  element.addEventListener('hidden.bs.toast', () => element.remove());
}

// 2. Add to Cart AJAX Helper
async function addToCart(productId, quantity = 1) {
  try {
    const response = await fetch('/Cart/Add', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': getCsrfToken()
      },
      body: JSON.stringify({ productId, quantity })
    });

    const result = await response.json();
    if (result.success) {
      showToast(result.message || 'تمت إضافة المنتج إلى سلة المشتريات بنجاح', 'success');
      updateCartBadge(result.totalItems);
    } else {
      showToast(result.message || 'تعذر إضافة المنتج إلى السلة', 'danger');
    }
  } catch (err) {
    console.error('Add to cart error:', err);
    showToast('حدث خطأ أثناء إضافة المنتج إلى السلة', 'danger');
  }
}

function updateCartBadge(count) {
  const badge = document.getElementById('cart-badge');
  if (badge) {
    badge.innerText = count;
    badge.style.display = count > 0 ? 'inline-block' : 'none';
  }
}

function getCsrfToken() {
  const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
  return tokenInput ? tokenInput.value : '';
}

// 3. Reels Scroll Autoplay Observer
function initReelsObserver() {
  const reels = document.querySelectorAll('.reel-item video');
  if (!reels.length) return;

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      const video = entry.target;
      if (entry.isIntersecting && entry.intersectionRatio >= 0.6) {
        video.play().catch(e => console.log('Autoplay restricted:', e));
      } else {
        video.pause();
      }
    });
  }, { threshold: 0.6 });

  reels.forEach(video => {
    observer.observe(video);
    // Click to toggle play/pause
    video.addEventListener('click', () => {
      if (video.paused) {
        video.play();
      } else {
        video.pause();
      }
    });
  });
}

// 4. Like Reel AJAX
async function toggleLikeReel(btn, videoId) {
  try {
    const response = await fetch(`/Reels/Like/${videoId}`, {
      method: 'POST',
      headers: {
        'RequestVerificationToken': getCsrfToken()
      }
    });
    const data = await response.json();
    if (data.success) {
      const countSpan = btn.querySelector('.like-count');
      const icon = btn.querySelector('i');
      if (countSpan) countSpan.innerText = data.likesCount;
      if (data.isLiked) {
        icon.className = 'bi bi-heart-fill text-danger';
        showToast('تم الإعجاب بالفيديو', 'success');
      } else {
        icon.className = 'bi bi-heart';
      }
    } else {
      showToast(data.message || 'يرجى تسجيل الدخول أولاً للإعجاب', 'info');
    }
  } catch (e) {
    console.error(e);
  }
}

// 5. Dark Mode Toggle
function toggleTheme() {
  const current = document.documentElement.getAttribute('data-bs-theme');
  const next = current === 'dark' ? 'light' : 'dark';
  document.documentElement.setAttribute('data-bs-theme', next);
  localStorage.setItem('souqna_theme', next);
  updateThemeIcon(next);
}

function updateThemeIcon(theme) {
  const icon = document.getElementById('theme-toggle-icon');
  if (icon) {
    icon.className = theme === 'dark' ? 'bi bi-sun-fill text-warning' : 'bi bi-moon-stars-fill text-white';
  }
}

// Run on load
document.addEventListener('DOMContentLoaded', () => {
  const savedTheme = localStorage.getItem('souqna_theme') || 'light';
  document.documentElement.setAttribute('data-bs-theme', savedTheme);
  updateThemeIcon(savedTheme);

  initReelsObserver();
});
