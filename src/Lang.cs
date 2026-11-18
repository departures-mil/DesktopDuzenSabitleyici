namespace DDS;

public static class Lang
{
    public static string Current { get; set; } = "tr";
    public static bool IsRtl => Current == "ar";

    public static readonly (string code, string name)[] Languages =
        { ("tr", "Türkçe"), ("en", "English"), ("ar", "العربية") };

    static readonly Dictionary<string, (string tr, string en, string ar)> S = new()
    {
        ["app_title"] = ("Desktop Düzen Sabitleyici", "Desktop Layout Keeper", "مثبّت ترتيب سطح المكتب"),
        ["status_hdr"] = ("Durum", "Status", "الحالة"),
        ["prot_on"] = ("Koruma AÇIK — masaüstü düzeniniz korunuyor", "Protection ON — your desktop layout is safe", "الحماية مفعّلة — ترتيب سطح المكتب محفوظ"),
        ["prot_off"] = ("Koruma KAPALI — düzen değişirse geri yüklenmez", "Protection OFF — changes will not be restored", "الحماية متوقفة — لن تتم استعادة الترتيب"),
        ["btn_prot_off"] = ("Korumayı durdur", "Pause protection", "إيقاف الحماية"),
        ["btn_prot_on"] = ("Korumayı başlat", "Start protection", "تشغيل الحماية"),
        ["screens"] = ("Ekranlar", "Displays", "الشاشات"),
        ["primary"] = ("Ana", "Main", "رئيسية"),
        ["icons"] = ("Masaüstü simgesi: {0}", "Desktop icons: {0}", "أيقونات سطح المكتب: {0}"),
        ["last_save"] = ("Son kayıt: {0}", "Last saved: {0}", "آخر حفظ: {0}"),
        ["last_restore"] = ("Son geri yükleme: {0}", "Last restored: {0}", "آخر استعادة: {0}"),
        ["never"] = ("henüz yok", "never", "لا يوجد بعد"),
        ["save_now"] = ("Şimdiki düzeni kaydet", "Save current layout", "احفظ الترتيب الحالي"),
        ["restore_now"] = ("Kayıtlı düzeni geri yükle", "Restore saved layout", "استعادة الترتيب المحفوظ"),
        ["how_it_works"] = ("Simgeleri istediğiniz gibi yerleştirin; uygulama düzeni kendiliğinden kaydeder. Ekran, çözünürlük veya ölçek değiştiğinde düzeni birkaç saniye içinde geri getirir.",
                            "Arrange your icons the way you like; the app saves the layout by itself. When the display, resolution or scaling changes, it brings the layout back within seconds.",
                            "رتّب الأيقونات كما تريد، وسيحفظ التطبيق الترتيب تلقائياً. عند تغيّر الشاشة أو الدقة أو التحجيم يعيد الترتيب خلال ثوانٍ."),
        ["settings_hdr"] = ("Ayarlar", "Settings", "الإعدادات"),
        ["mode"] = ("Ekran değişince:", "When the display changes:", "عند تغيّر الشاشة:"),
        ["mode_prop"] = ("Oransal — aynı düzeni yeni ekrana orantılı taşı", "Proportional — scale the same layout to the new screen", "نسبي — انقل الترتيب نفسه بشكل متناسب إلى الشاشة الجديدة"),
        ["mode_prof"] = ("Ekran başına profil — her ekranın kendi düzenini hatırla", "Per-display profile — remember each screen's own layout", "ملف لكل شاشة — تذكّر ترتيب كل شاشة على حدة"),
        ["startup"] = ("Windows ile birlikte başlat (arka planda)", "Start with Windows (in the background)", "التشغيل مع ويندوز (في الخلفية)"),
        ["autoarr"] = ("Windows'un otomatik simge düzenlemesini kapat", "Turn off Windows auto-arrange", "إيقاف الترتيب التلقائي في ويندوز"),
        ["language"] = ("Dil", "Language", "اللغة"),
        ["profiles_hdr"] = ("Kayıtlı ekran düzenleri", "Saved display layouts", "ترتيبات الشاشات المحفوظة"),
        ["col_display"] = ("Ekran", "Display", "الشاشة"),
        ["col_icons"] = ("Simge", "Icons", "الأيقونات"),
        ["col_date"] = ("Tarih", "Date", "التاريخ"),
        ["delete"] = ("Seçileni sil", "Delete selected", "حذف المحدد"),
        ["current"] = ("şu anki", "current", "الحالية"),
        ["log_hdr"] = ("Olaylar", "Activity", "السجل"),
        ["tt_update"] = ("Güncelle", "Update", "تحديث"),
        ["tt_update_new"] = ("Yeni sürüm hazır: v{0} — güncellemek için tıklayın", "New version ready: v{0} — click to update", "إصدار جديد جاهز: v{0} — انقر للتحديث"),
        ["tt_sync"] = ("GitHub ile eşitle", "Sync with GitHub", "مزامنة مع GitHub"),
        ["tt_min"] = ("Küçült", "Minimize", "تصغير"),
        ["tt_half"] = ("Orta boyut", "Half size", "حجم متوسط"),
        ["tt_full"] = ("Tam ekran", "Full screen", "ملء الشاشة"),
        ["tt_close"] = ("Kapat (arka planda korumaya devam eder)", "Close (keeps protecting in the background)", "إغلاق (يستمر بالحماية في الخلفية)"),
        ["tray_open"] = ("Aç", "Open", "فتح"),
        ["tray_exit"] = ("Tamamen çık", "Exit completely", "خروج نهائي"),
        ["tray_bg"] = ("Arka planda çalışmaya devam ediyor. Masaüstü düzeniniz korunuyor.", "Still running in the background. Your desktop layout is protected.", "يعمل في الخلفية. ترتيب سطح المكتب محمي."),
        ["log_started"] = ("Başlatıldı. Ekran: {0}", "Started. Display: {0}", "بدأ التشغيل. الشاشة: {0}"),
        ["log_saved"] = ("Düzen kaydedildi ({0} simge)", "Layout saved ({0} icons)", "تم حفظ الترتيب ({0} أيقونة)"),
        ["log_changed"] = ("Ekran değişti: {0}", "Display changed: {0}", "تغيّرت الشاشة: {0}"),
        ["log_restored"] = ("Düzen geri yüklendi ({0} simge, {1})", "Layout restored ({0} icons, {1})", "تمت استعادة الترتيب ({0} أيقونة، {1})"),
        ["how_exact"] = ("aynı konumlar", "exact positions", "المواضع نفسها"),
        ["how_prop"] = ("oransal", "proportional", "نسبي"),
        ["log_explorer"] = ("Masaüstü yeniden başladı, düzen geri yükleniyor", "Desktop restarted, restoring layout", "أُعيد تشغيل سطح المكتب، تتم استعادة الترتيب"),
        ["log_resume"] = ("Uyku modundan çıkıldı, ekran denetleniyor", "Resumed from sleep, checking displays", "استئناف من السكون، يتم فحص الشاشات"),
        ["log_noLayout"] = ("Henüz kayıtlı düzen yok", "No saved layout yet", "لا يوجد ترتيب محفوظ بعد"),
        ["log_err"] = ("Hata: {0}", "Error: {0}", "خطأ: {0}"),
        ["log_synced"] = ("GitHub ile eşitlendi", "Synced with GitHub", "تمت المزامنة مع GitHub"),
        ["log_update"] = ("Yeni sürüm bulundu: v{0}", "New version found: v{0}", "تم العثور على إصدار جديد: v{0}"),
        ["upd_title"] = ("Güncelleme", "Update", "التحديث"),
        ["upd_checking"] = ("Güncellemeler denetleniyor…", "Checking for updates…", "جارٍ التحقق من التحديثات…"),
        ["upd_latest"] = ("En güncel sürümü kullanıyorsunuz (v{0}).", "You are on the latest version (v{0}).", "أنت تستخدم أحدث إصدار (v{0})."),
        ["upd_downloading"] = ("v{0} indiriliyor…", "Downloading v{0}…", "جارٍ تنزيل v{0}…"),
        ["upd_progress"] = ("%{0}   •   {1:F1} MB / {2:F1} MB   •   Kalan: {3:F1} MB   •   Hız: {4:F2} MB/sn",
                            "{0}%   •   {1:F1} MB / {2:F1} MB   •   Remaining: {3:F1} MB   •   Speed: {4:F2} MB/s",
                            "{0}%   •   {1:F1} MB / {2:F1} MB   •   المتبقي: {3:F1} MB   •   السرعة: {4:F2} MB/ث"),
        ["upd_installing"] = ("Kuruluyor, uygulama yeniden açılacak…", "Installing, the app will restart…", "جارٍ التثبيت، سيُعاد تشغيل التطبيق…"),
        ["upd_fail"] = ("Güncelleme yapılamadı: {0}", "Update failed: {0}", "تعذّر التحديث: {0}"),
        ["cancel"] = ("İptal", "Cancel", "إلغاء"),
        ["ok"] = ("Tamam", "OK", "موافق"),
        ["sync_title"] = ("GitHub eşitleme", "GitHub sync", "مزامنة GitHub"),
        ["sync_msg"] = ("Düzenlerinizi GitHub hesabınızda gizli bir yedekte saklamak ve diğer bilgisayarlarla eşitlemek için bir kereliğine erişim anahtarı gerekir.\n\n1) 'Anahtar oluştur' butonuna basın (sayfa hazır açılır)\n2) Sayfanın altındaki 'Generate token' butonuna basın\n3) Çıkan kodu kopyalayıp aşağıya yapıştırın\n\nBu işlem sadece bir kez yapılır.",
                        "To keep your layouts in a private backup on your GitHub account and sync them with your other PCs, a one-time access key is needed.\n\n1) Press 'Create key' (the page opens pre-filled)\n2) Press 'Generate token' at the bottom of the page\n3) Copy the code and paste it below\n\nYou only do this once.",
                        "لحفظ ترتيباتك في نسخة احتياطية خاصة على حساب GitHub ومزامنتها مع أجهزتك الأخرى، يلزم مفتاح وصول لمرة واحدة.\n\n1) اضغط 'إنشاء مفتاح' (تُفتح الصفحة جاهزة)\n2) اضغط 'Generate token' أسفل الصفحة\n3) انسخ الرمز والصقه أدناه\n\nيتم ذلك مرة واحدة فقط."),
        ["sync_create"] = ("Anahtar oluştur", "Create key", "إنشاء مفتاح"),
        ["sync_ok"] = ("Eşitleme tamamlandı.", "Sync complete.", "اكتملت المزامنة."),
        ["sync_fail"] = ("Eşitleme yapılamadı: {0}", "Sync failed: {0}", "تعذّرت المزامنة: {0}"),
        ["sync_working"] = ("Eşitleniyor…", "Syncing…", "جارٍ المزامنة…"),
    };

    public static string T(string key)
    {
        if (!S.TryGetValue(key, out var v)) return key;
        return Current switch { "en" => v.en, "ar" => v.ar, _ => v.tr };
    }

    public static string F(string key, params object[] args)
    {
        try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, T(key), args); }
        catch { return T(key); }
    }
}
