const CACHE = 'kenketsu-note-v1';
const PRECACHE = ['/image/logo1.png', '/image/logo2.png', '/image/logo1-ogp.png'];

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(PRECACHE)));
    self.skipWaiting();
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', e => {
    const url = new URL(e.request.url);
    // API・動的ページはネットワーク優先
    if (url.pathname.startsWith('/Tracker/') || url.pathname.startsWith('/Stamp/') || url.pathname.startsWith('/pwa/')) {
        e.respondWith(fetch(e.request).catch(() => caches.match(e.request)));
        return;
    }
    // 静的アセットはキャッシュ優先
    e.respondWith(caches.match(e.request).then(r => r || fetch(e.request)));
});
