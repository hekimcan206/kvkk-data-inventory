# Personal data inventory (VERBİS-style)

- Data controller: **Örnek Klinik Sağlık Hizmetleri A.Ş. (synthetic example)**
- Source: `oracle` schema `CLINIC`
- Generated: 2026-11-07 10:30 UTC
- Entries: 25 (11 special-category)

| Data subject group | Data category | Special | Purposes | Legal basis | Max. retention | Recipient groups | Transfer abroad | Found in |
|---|---|---|---|---|---|---|---|---|
| Diğer | İletişim | no | **missing** | **missing** | **missing** | **missing** | no | SUBELER.ADRES (Address)<br>SUBELER.TELEFON (Phone) |
| Hasta | Felsefi İnanç, Din, Mezhep ve Diğer İnançlar | **yes** | **missing** | **missing** | **missing** | **missing** | no | SOSYAL_DEGERLENDIRME.MEZHEP (ReligionBelief) |
| Hasta | İletişim | no | İletişim faaliyetlerinin yürütülmesi; Randevu hatırlatma | KVKK m.5/2-c (sözleşmenin ifası) | Son işlemden itibaren 10 yıl (illustrative) | Tedarikçiler (SMS hizmet sağlayıcısı) | no | HASTALAR.ADRES (Address)<br>HASTALAR.CEP_TELEFONU (Phone)<br>HASTALAR.EPOSTA (Email)<br>RANDEVULAR.NOTLAR (Phone) |
| Hasta | Irk ve Etnik Köken | **yes** | **missing** | **missing** | **missing** | **missing** | no | SOSYAL_DEGERLENDIRME.ETNIK_KOKEN (EthnicOrigin) |
| Hasta | Finans | no | Finans ve muhasebe işlerinin yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük) | 10 yıl (VUK ve TTK saklama süreleri, illustrative) | Yetkili kamu kurum ve kuruluşları; Bankalar | no | FATURALAR.ODEME_KARTI (PaymentCard) |
| Hasta | Genetik Veri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 | 20 yıl (illustrative) | Tedarikçiler (referans genetik laboratuvarı) | yes: Almanya | GENETIK_TESTLER.DNA_ORNEK_KODU (Genetic) |
| Hasta | Sağlık Bilgileri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 (sır saklama yükümlülüğü altındaki kişilerce tıbbi teşhis ve tedavi) | 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Sağlık Bakanlığı, SGK) | no | HASTALAR.KAN_GRUBU (Health)<br>RANDEVULAR.NOTLAR (Health)<br>RANDEVULAR.ODA_NO (Health)<br>TANILAR.ICD10_KODU (Health)<br>TANILAR.TANI_ACIKLAMASI (Health) |
| Hasta | Kimlik | no | Sağlık hizmetlerinin yürütülmesi; Faaliyetlerin mevzuata uygun yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük); KVKK m.5/2-c (sözleşmenin ifası) | Son işlemden itibaren 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları; Özel sigorta şirketleri | no | FATURALAR.SIGORTA_VKN (TaxId)<br>HASTALAR.AD (PersonName)<br>HASTALAR.CINSIYET (Gender)<br>HASTALAR.DOGUM_TARIHI (DateOfBirth)<br>HASTALAR.SOYAD (PersonName)<br>HASTALAR.TC_KIMLIK_NO (NationalId)<br>RANDEVULAR.DOKTOR_ADI (PersonName) |
| Hasta | Siyasi Düşünce | **yes** | **missing** | **missing** | **missing** | **missing** | no | SOSYAL_DEGERLENDIRME.SIYASI_GORUS (PoliticalOpinion) |
| Online Portal Kullanıcısı | İletişim | no | İletişim faaliyetlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | Üyelik sona erdikten sonra 3 yıl (illustrative) | Tedarikçiler (e-posta hizmet sağlayıcısı) | yes: İrlanda | CUSTOMERS.EMAIL_ADDRESS (Email)<br>CUSTOMERS.PHONE_NUMBER (Phone) |
| Online Portal Kullanıcısı | Finans | no | Finans ve muhasebe işlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | 10 yıl (illustrative) | Ödeme kuruluşları | no | CUSTOMERS.CARD_NUMBER (PaymentCard) |
| Online Portal Kullanıcısı | Kimlik | no | Ürün / hizmetlerin satış süreçlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | Üyelik sona erdikten sonra 3 yıl (illustrative) | **missing** | no | CUSTOMERS.FULL_NAME (PersonName) |
| Online Portal Kullanıcısı | İşlem Güvenliği | no | Bilgi güvenliği süreçlerinin yürütülmesi | KVKK m.5/2-ç (5651 sayılı Kanun) | 2 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları | no | CUSTOMERS.LAST_LOGIN_IP (IpAddress) |
| Çalışan | Dernek Üyeliği | **yes** | **missing** | **missing** | **missing** | **missing** | no | PERSONEL.DERNEK_UYELIKLERI (AssociationMembership) |
| Çalışan | Felsefi İnanç, Din, Mezhep ve Diğer İnançlar | **yes** | **missing** | **missing** | **missing** | **missing** | no | PERSONEL.DIN (ReligionBelief) |
| Çalışan | Biyometrik Veri | **yes** | Fiziksel mekân güvenliğinin temini (yüksek güvenlikli alan girişi) | KVKK m.6/3 (açık rıza) | İş ilişkisinin sona ermesiyle derhal imha (illustrative) | **missing** | no | PERSONEL.PARMAK_IZI_SABLONU (Biometric) |
| Çalışan | İletişim | no | İletişim faaliyetlerinin yürütülmesi; Acil durum yönetimi süreçlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | **missing** | no | PERSONEL.ACIL_DURUM_TEL (Phone)<br>PERSONEL.EMAIL (Email)<br>PERSONEL.GSM (Phone)<br>PERSONEL.IKAMET_ADRESI (Address) |
| Çalışan | Ceza Mahkûmiyeti ve Güvenlik Tedbirleri | **yes** | Çalışan adayı / çalışan seçme ve yerleştirme süreçlerinin yürütülmesi (yalnızca kanunen zorunlu pozisyonlar) | KVKK m.6/3 (kanunlarda öngörülme) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | **missing** | no | PERSONEL.SABIKA_KAYDI (CriminalRecord) |
| Çalışan | Finans | no | Ücret politikasının yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük) | 10 yıl (illustrative) | Bankalar | no | PERSONEL.IBAN (Iban) |
| Çalışan | Sağlık Bilgileri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 (sır saklama yükümlülüğü altındaki kişilerce tıbbi teşhis ve tedavi) | 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Sağlık Bakanlığı, SGK) | no | PERSONEL.ENGEL_DURUMU (Health)<br>PERSONEL.KAN_GRUBU (Health)<br>PERSONEL_IZINLERI.RAPOR_TANI_KODU (Health) |
| Çalışan | Kimlik | no | Çalışanlar için iş akdi ve mevzuattan kaynaklı yükümlülüklerin yerine getirilmesi | KVKK m.5/2-ç (hukuki yükümlülük); KVKK m.5/2-c (sözleşmenin ifası) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (SGK, İŞKUR) | no | PERSONEL.ACIL_DURUM_KISI (PersonName)<br>PERSONEL.AD_SOYAD (PersonName)<br>PERSONEL.BIRTH_DATE (DateOfBirth)<br>PERSONEL.GENDER (Gender)<br>PERSONEL.TCKN (NationalId) |
| Çalışan | Lokasyon | no | Fiziksel mekân güvenliğinin temini (otopark girişi) | KVKK m.5/2-f (meşru menfaat) | İş ilişkisinin sona ermesiyle imha (illustrative) | **missing** | no | PERSONEL.ARAC_PLAKASI (VehiclePlate) |
| Çalışan | Özlük | no | Ücret politikasının yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | 10 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Gelir İdaresi, SGK) | no | PERSONEL.MAAS (Salary) |
| Çalışan | İşlem Güvenliği | no | Bilgi güvenliği süreçlerinin yürütülmesi | KVKK m.5/2-ç (5651 sayılı Kanun); KVKK m.5/2-f (meşru menfaat) | 2 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları | no | AUDIT_LOG.IP_ADRESI (IpAddress) |
| Çalışan | Sendika Üyeliği | **yes** | Çalışanlar için iş akdi ve mevzuattan kaynaklı yükümlülüklerin yerine getirilmesi (sendika aidatı kesintisi) | KVKK m.6/3 (kanunlarda öngörülme — 6356 sayılı Kanun) | Üyelik sona erdikten sonra 10 yıl (illustrative) | İlgili sendika | no | PERSONEL.SENDIKA_UYELIGI (UnionMembership) |

## Gaps

- Diğer / İletişim: found in 2 column(s) but no purpose, legal basis or retention period is documented.
- Tables without a data subject group: SUBELER.
- Hasta / Felsefi İnanç, Din, Mezhep ve Diğer İnançlar: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Hasta / Irk ve Etnik Köken: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Hasta / Siyasi Düşünce: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Çalışan / Dernek Üyeliği: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Çalışan / Felsefi İnanç, Din, Mezhep ve Diğer İnançlar: found in 1 column(s) but no purpose, legal basis or retention period is documented.
