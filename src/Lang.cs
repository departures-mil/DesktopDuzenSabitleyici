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
        ["startup"] = ("Bilgisayar her açıldığında otomatik başlat (arka planda)", "Start automatically every time the PC starts (in the background)", "التشغيل تلقائياً عند كل تشغيل للكمبيوتر (في الخلفية)"),
        ["autoarr"] = ("Windows'un otomatik simge düzenlemesini kapat", "Turn off Windows auto-arrange", "إيقاف الترتيب التلقائي في ويندوز"),
        ["language"] = ("Dil", "Language", "اللغة"),
        ["profiles_hdr"] = ("Kayıt geçmişi (her ekranın kendi kayıtları)", "Save history (each display has its own)", "سجل الحفظ (لكل شاشة سجلها)"),
        ["col_src"] = ("Kaynak", "Source", "المصدر"),
        ["restore_sel"] = ("Seçileni geri yükle", "Restore selected", "استعادة المحدد"),
        ["src_user"] = ("Elle düzenleme", "Your arrangement", "ترتيبك"),
        ["src_manual"] = ("Elle kayıt", "Manual save", "حفظ يدوي"),
        ["src_hotkey"] = ("Kısayol (Ctrl+Alt+S)", "Shortcut (Ctrl+Alt+S)", "اختصار (Ctrl+Alt+S)"),
        ["src_auto"] = ("Otomatik (30 dk)", "Auto (30 min)", "تلقائي (30 دقيقة)"),
        ["src_presave"] = ("Ekran değişmeden önce", "Before display change", "قبل تغيّر الشاشة"),
        ["src_shutdown"] = ("Kapanmadan önce", "Before shutdown", "قبل الإيقاف"),
        ["src_sleep"] = ("Uykudan önce", "Before sleep", "قبل السكون"),
        ["src_old"] = ("Eski sürüm kaydı", "Old version record", "سجل من إصدار سابق"),
        ["mode_info"] = ("Her ekran düzeni (laptop, büyük monitör, farklı çözünürlük veya ölçek) kendi kaydını ayrı tutar ve kendi kaydına geri döner. Hiç kaydı olmayan yeni bir ekranda, en yakın kayıt kenarlara göre uyarlanır: sağ kenardaki simgeler sağda, sol üsttekiler sol üstte kalır, araları açılmaz. Windows'un karıştırdığı düzen asla kaydedilmez.",
                         "Every display setup (laptop, big monitor, other resolution or scaling) keeps its own records and goes back to its own layout. On a new screen with no record, the closest record is adapted to the edges: icons at the right edge stay at the right, top-left stays top-left, and gaps are not stretched. A layout shuffled by Windows is never saved.",
                         "يحتفظ كل إعداد شاشة (الحاسوب المحمول، الشاشة الكبيرة، دقة أو تحجيم مختلف) بسجلاته الخاصة ويعود إلى ترتيبه. على شاشة جديدة بلا سجل، يُكيَّف أقرب سجل حسب الحواف: تبقى أيقونات الحافة اليمنى يميناً والأعلى يساراً في مكانها دون تباعد. لا يُحفظ أبداً ترتيب بعثره ويندوز."),
        ["log_reshuffle"] = ("Windows simgeleri karıştırdı, kayıtlı düzen geri getiriliyor", "Windows shuffled the icons, bringing the saved layout back", "بعثر ويندوز الأيقونات، تتم إعادة الترتيب المحفوظ"),
        ["log_adapted"] = ("Bu ekran için kayıt yoktu: en yakın kayıt uyarlandı. Bir simgeyi taşıdığınızda bu ekranın kendi kaydı oluşur.", "No record for this display: the closest one was adapted. Move any icon and this display gets its own record.", "لا يوجد سجل لهذه الشاشة: تم تكييف أقرب سجل. حرّك أي أيقونة لإنشاء سجل خاص بها."),
        ["log_migrated"] = ("Eski kayıtlar yeni biçime aktarıldı ({0} kayıt). Kayıt geçmişinden seçip geri yükleyebilirsiniz.", "Old records were converted ({0}). You can pick and restore them from the history.", "تم تحويل السجلات القديمة ({0}). يمكنك اختيارها واستعادتها من السجل."),
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
        ["how_prop"] = ("başka ekrandan uyarlandı", "adapted from another display", "مُكيَّف من شاشة أخرى"),
        ["log_explorer"] = ("Masaüstü yeniden başladı, düzen geri yükleniyor", "Desktop restarted, restoring layout", "أُعيد تشغيل سطح المكتب، تتم استعادة الترتيب"),
        ["log_resume"] = ("Uyku modundan çıkıldı, ekran denetleniyor", "Resumed from sleep, checking displays", "استئناف من السكون، يتم فحص الشاشات"),
        ["log_noLayout"] = ("Henüz kayıtlı düzen yok", "No saved layout yet", "لا يوجد ترتيب محفوظ بعد"),
        ["log_err"] = ("Hata: {0}", "Error: {0}", "خطأ: {0}"),
        ["log_synced"] = ("GitHub ile eşitlendi", "Synced with GitHub", "تمت المزامنة مع GitHub"),
        ["log_update"] = ("Yeni sürüm bulundu: v{0}", "New version found: v{0}", "تم العثور على إصدار جديد: v{0}"),
        ["log_autosave"] = ("Otomatik kayıt (30 dk): {0} simge", "Auto-save (30 min): {0} icons", "حفظ تلقائي (30 دقيقة): {0} أيقونة"),
        ["log_presave"] = ("Ekran değişmeden önceki düzen korundu ({0} simge)", "Layout before the display change was preserved ({0} icons)", "تم الحفاظ على الترتيب قبل تغيّر الشاشة ({0} أيقونة)"),
        ["log_shutdown"] = ("Bilgisayar kapanmadan önce düzen kaydedildi ({0} simge)", "Layout saved before shutdown ({0} icons)", "تم حفظ الترتيب قبل إيقاف التشغيل ({0} أيقونة)"),
        ["log_sleep"] = ("Uyku modundan önce düzen kaydedildi ({0} simge)", "Layout saved before sleep ({0} icons)", "تم حفظ الترتيب قبل السكون ({0} أيقونة)"),
        ["log_hotkey"] = ("Kısayol tuşuyla kaydedildi", "Saved with the shortcut key", "تم الحفظ بمفتاح الاختصار"),
        ["autosave"] = ("Her 30 dakikada bir otomatik kaydet", "Auto-save every 30 minutes", "حفظ تلقائي كل 30 دقيقة"),
        ["save_info"] = ("Ayrıca: her değişiklikte, bilgisayar kapanmadan / uykuya geçmeden önce ve ekran değişmeden önce kendiliğinden kaydeder. Elle kaydetme kısayolu: Ctrl + Alt + S",
                         "Also saves automatically on every change, before shutdown / sleep and before the display changes. Manual save shortcut: Ctrl + Alt + S",
                         "يحفظ تلقائياً أيضاً عند كل تغيير، وقبل إيقاف التشغيل / السكون، وقبل تغيّر الشاشة. اختصار الحفظ اليدوي: Ctrl + Alt + S"),
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
