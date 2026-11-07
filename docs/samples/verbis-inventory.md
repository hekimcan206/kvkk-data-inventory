# Personal data inventory (VERBİS-style)

- Data controller: **Örnek Klinik Sağlık Hizmetleri A.Ş. (synthetic example)**
- Source: `memory` schema `clinic`
- Generated: 2026-11-07 00:47 UTC
- Entries: 25 (11 special-category)

| Data subject group | Data category | Special | Purposes | Legal basis | Max. retention | Recipient groups | Transfer abroad | Found in |
|---|---|---|---|---|---|---|---|---|
| Diğer | İletişim | no | **missing** | **missing** | **missing** | **missing** | no | subeler.adres (Address)<br>subeler.telefon (Phone) |
| Hasta | Felsefi İnanç, Din, Mezhep ve Diğer İnançlar | **yes** | **missing** | **missing** | **missing** | **missing** | no | sosyal_degerlendirme.mezhep (ReligionBelief) |
| Hasta | İletişim | no | İletişim faaliyetlerinin yürütülmesi; Randevu hatırlatma | KVKK m.5/2-c (sözleşmenin ifası) | Son işlemden itibaren 10 yıl (illustrative) | Tedarikçiler (SMS hizmet sağlayıcısı) | no | hastalar.adres (Address)<br>hastalar.cep_telefonu (Phone)<br>hastalar.eposta (Email)<br>randevular.notlar (Phone) |
| Hasta | Irk ve Etnik Köken | **yes** | **missing** | **missing** | **missing** | **missing** | no | sosyal_degerlendirme.etnik_koken (EthnicOrigin) |
| Hasta | Finans | no | Finans ve muhasebe işlerinin yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük) | 10 yıl (VUK ve TTK saklama süreleri, illustrative) | Yetkili kamu kurum ve kuruluşları; Bankalar | no | faturalar.odeme_karti (PaymentCard) |
| Hasta | Genetik Veri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 | 20 yıl (illustrative) | Tedarikçiler (referans genetik laboratuvarı) | yes: Almanya | genetik_testler.dna_ornek_kodu (Genetic) |
| Hasta | Sağlık Bilgileri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 (sır saklama yükümlülüğü altındaki kişilerce tıbbi teşhis ve tedavi) | 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Sağlık Bakanlığı, SGK) | no | hastalar.kan_grubu (Health)<br>randevular.notlar (Health)<br>randevular.oda_no (Health)<br>tanilar.icd10_kodu (Health)<br>tanilar.tani_aciklamasi (Health) |
| Hasta | Kimlik | no | Sağlık hizmetlerinin yürütülmesi; Faaliyetlerin mevzuata uygun yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük); KVKK m.5/2-c (sözleşmenin ifası) | Son işlemden itibaren 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları; Özel sigorta şirketleri | no | faturalar.sigorta_vkn (TaxId)<br>hastalar.ad (PersonName)<br>hastalar.cinsiyet (Gender)<br>hastalar.dogum_tarihi (DateOfBirth)<br>hastalar.soyad (PersonName)<br>hastalar.tc_kimlik_no (NationalId)<br>randevular.doktor_adi (PersonName) |
| Hasta | Siyasi Düşünce | **yes** | **missing** | **missing** | **missing** | **missing** | no | sosyal_degerlendirme.siyasi_gorus (PoliticalOpinion) |
| Online Portal Kullanıcısı | İletişim | no | İletişim faaliyetlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | Üyelik sona erdikten sonra 3 yıl (illustrative) | Tedarikçiler (e-posta hizmet sağlayıcısı) | yes: İrlanda | customers.email_address (Email)<br>customers.phone_number (Phone) |
| Online Portal Kullanıcısı | Finans | no | Finans ve muhasebe işlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | 10 yıl (illustrative) | Ödeme kuruluşları | no | customers.card_number (PaymentCard) |
| Online Portal Kullanıcısı | Kimlik | no | Ürün / hizmetlerin satış süreçlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | Üyelik sona erdikten sonra 3 yıl (illustrative) | **missing** | no | customers.full_name (PersonName) |
| Online Portal Kullanıcısı | İşlem Güvenliği | no | Bilgi güvenliği süreçlerinin yürütülmesi | KVKK m.5/2-ç (5651 sayılı Kanun) | 2 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları | no | customers.last_login_ip (IpAddress) |
| Çalışan | Dernek Üyeliği | **yes** | **missing** | **missing** | **missing** | **missing** | no | personel.dernek_uyelikleri (AssociationMembership) |
| Çalışan | Felsefi İnanç, Din, Mezhep ve Diğer İnançlar | **yes** | **missing** | **missing** | **missing** | **missing** | no | personel.din (ReligionBelief) |
| Çalışan | Biyometrik Veri | **yes** | Fiziksel mekân güvenliğinin temini (yüksek güvenlikli alan girişi) | KVKK m.6/3 (açık rıza) | İş ilişkisinin sona ermesiyle derhal imha (illustrative) | **missing** | no | personel.parmak_izi_sablonu (Biometric) |
| Çalışan | İletişim | no | İletişim faaliyetlerinin yürütülmesi; Acil durum yönetimi süreçlerinin yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | **missing** | no | personel.acil_durum_tel (Phone)<br>personel.email (Email)<br>personel.gsm (Phone)<br>personel.ikamet_adresi (Address) |
| Çalışan | Ceza Mahkûmiyeti ve Güvenlik Tedbirleri | **yes** | Çalışan adayı / çalışan seçme ve yerleştirme süreçlerinin yürütülmesi (yalnızca kanunen zorunlu pozisyonlar) | KVKK m.6/3 (kanunlarda öngörülme) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | **missing** | no | personel.sabika_kaydi (CriminalRecord) |
| Çalışan | Finans | no | Ücret politikasının yürütülmesi | KVKK m.5/2-ç (hukuki yükümlülük) | 10 yıl (illustrative) | Bankalar | no | personel.iban (Iban) |
| Çalışan | Sağlık Bilgileri | **yes** | Tıbbi teşhis ve tedavi hizmetlerinin yürütülmesi | KVKK m.6/3 (sır saklama yükümlülüğü altındaki kişilerce tıbbi teşhis ve tedavi) | 20 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Sağlık Bakanlığı, SGK) | no | personel.engel_durumu (Health)<br>personel.kan_grubu (Health)<br>personel_izinleri.rapor_tani_kodu (Health) |
| Çalışan | Kimlik | no | Çalışanlar için iş akdi ve mevzuattan kaynaklı yükümlülüklerin yerine getirilmesi | KVKK m.5/2-ç (hukuki yükümlülük); KVKK m.5/2-c (sözleşmenin ifası) | İş ilişkisinin sona ermesinden itibaren 10 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (SGK, İŞKUR) | no | personel.acil_durum_kisi (PersonName)<br>personel.ad_soyad (PersonName)<br>personel.birth_date (DateOfBirth)<br>personel.gender (Gender)<br>personel.tckn (NationalId) |
| Çalışan | Lokasyon | no | Fiziksel mekân güvenliğinin temini (otopark girişi) | KVKK m.5/2-f (meşru menfaat) | İş ilişkisinin sona ermesiyle imha (illustrative) | **missing** | no | personel.arac_plakasi (VehiclePlate) |
| Çalışan | Özlük | no | Ücret politikasının yürütülmesi | KVKK m.5/2-c (sözleşmenin ifası) | 10 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları (Gelir İdaresi, SGK) | no | personel.maas (Salary) |
| Çalışan | İşlem Güvenliği | no | Bilgi güvenliği süreçlerinin yürütülmesi | KVKK m.5/2-ç (5651 sayılı Kanun); KVKK m.5/2-f (meşru menfaat) | 2 yıl (illustrative) | Yetkili kamu kurum ve kuruluşları | no | audit_log.ip_adresi (IpAddress) |
| Çalışan | Sendika Üyeliği | **yes** | Çalışanlar için iş akdi ve mevzuattan kaynaklı yükümlülüklerin yerine getirilmesi (sendika aidatı kesintisi) | KVKK m.6/3 (kanunlarda öngörülme — 6356 sayılı Kanun) | Üyelik sona erdikten sonra 10 yıl (illustrative) | İlgili sendika | no | personel.sendika_uyeligi (UnionMembership) |

## Gaps

- Diğer / İletişim: found in 2 column(s) but no purpose, legal basis or retention period is documented.
- Tables without a data subject group: subeler.
- Hasta / Felsefi İnanç, Din, Mezhep ve Diğer İnançlar: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Hasta / Irk ve Etnik Köken: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Hasta / Siyasi Düşünce: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Çalışan / Dernek Üyeliği: found in 1 column(s) but no purpose, legal basis or retention period is documented.
- Çalışan / Felsefi İnanç, Din, Mezhep ve Diğer İnançlar: found in 1 column(s) but no purpose, legal basis or retention period is documented.
