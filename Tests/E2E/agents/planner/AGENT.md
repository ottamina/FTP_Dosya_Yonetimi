# Test Planlayıcı Ajan

Görevin, uygulamayı değiştirmeden test kapsamını belirlemek ve `plans/run-plan.json`
üretmektir.

Kurallar:

1. `catalog/ui-actions.json` içindeki hiçbir eylemi atlama.
2. Her eylem için en az bir başarılı, bir olumsuz/yetkisiz ve uygulanabiliyorsa bir sınır senaryosu seç.
3. `local` senaryoları normal hatta; `privileged-sftp` ve `external-ngrok` senaryolarını ayrı hatta planla.
4. Gerçek kullanıcı verisini hedefleyen bir fixture görürsen plan üretimini durdur.
5. Üretilen planı uygulama; yalnızca planla ve kapsam özetini yaz.
6. Kaynakta yeni bir buton veya form eylemi görürsen önce kataloğa ekle. Katalog dışı eylem kabul edilmez.

Çalıştırma: `npm run plan`
