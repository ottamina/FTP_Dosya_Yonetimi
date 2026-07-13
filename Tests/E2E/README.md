# İki Ajanlı Uçtan Uca Test Sistemi

Bu klasör iki ayrı sorumluluğu zorunlu kılar:

- Planlayıcı ajan, kaynak ve katalogdan değiştirilemez bir test planı üretir.
- Uygulayıcı ajan, planı değiştirmeden Playwright ile çalıştırır ve kanıt toplar.

## Hızlı kullanım

```powershell
cd Tests\E2E
npm install
npm run plan
npm run audit
npm test
```

Normal plan yalnızca `local` hattını içerir. Yönetici/OpenSSH veya internet/ngrok
gerektiren senaryolar açıkça seçilir:

```powershell
$env:E2E_LANES='local,privileged-sftp,external-ngrok'
npm run plan
```

Mevcut güvenli regresyon paketi yalnızca `__e2e_` önekli geçici dosya ve
klasörler oluşturur; her testten sonra bunları API üzerinden temizler. Yine de
test çalışırken aynı isimleri elle kullanmayın. Sunucu silme, kullanıcı/rol CRUD,
OpenSSH ve ngrok gibi geniş kapsamlı senaryolar için benzersiz test depolama kökü
ve test veritabanı kullanılmadan ayrıcalıklı hatları çalıştırmayın.

Şu anda otomatik çalışan yedi temel tarayıcı testi şunları doğrular:

- hatalı ve başarılı uygulama/FTP girişleri;
- parola gösterme/gizleme, arama, yenileme ve çıkış;
- dosya yükleme, metin önizleme ve gerçek indirme;
- dosya ve klasör yeniden adlandırma, uzantı koruma ve iptal;
- sürükle-bırak taşıma ve çift onaylı silme;
- dosya/veritabanı log sekmeleri ile yönetim ekranları arasında gezinme.

Planlayıcının ürettiği 123 yerel senaryonun tamamının çalıştırılmış olduğu iddia
edilmez. Katalog kapsamı ile fiilen otomatikleştirilen kapsam ayrı tutulur.

Katalog şu an arayüzün 29 eylemini ve yerel hatta 123 varyantı kapsar.
Yeni bir buton/işlem eklendiğinde `catalog/ui-actions.json` güncellenmeden kapsam
denetimi geçmemelidir.
