# Limit LLM — StickyCountdown

Widget desktop ringan untuk melacak **countdown reset limit pemakaian model AI** (ChatGPT, Claude, DeepSeek, Gemini, dll). Satu baris untuk satu model, lengkap dengan catatan bebas.

![Screenshot](screenshot.png)

## Fitur

- **Multi model** — satu baris per model, nama bisa langsung diketik. Default 1 baris, tinggal tambah manual pakai **+ Tambah model** (maks 10)
- **Hapus baris** — tombol **×** di setiap baris (atau klik kanan → Hapus baris)
- **Dua cara atur reset** (klik kanan baris → pilih):
  - **Hitung mundur ... jam** — misal isi `5` (bisa desimal, `4.5`), otomatis countdown 5 jam
  - **Sampai jam ...** — misal `22:00`, countdown sampai jam 10 malam (kalau sudah lewat, ditanya mau ke besok atau langsung SIAP)
- **Auto popup saat countdown habis** — widget otomatis muncul lagi (walau sedang diperkecil), baris berkedip, dan **tidak bisa diperkecil sampai kamu klik Done**. Jadi reminder-nya susah diabaikan
- **Minimize ke pill mungil** — tombol **−** menyusutkan widget jadi pill kecil biar tidak menghalangi produktivitas, klik **□** untuk buka lagi
- **Ukuran fleksibel** — seret tepi/sudut widget (ada grip di kanan bawah), ukuran tersimpan otomatis
- **Bagian catatan** — tulis info bebas, misal jam pakai ideal atau harga model yang beda per jam (contoh: diskon DeepSeek di jam tertentu)
- **Selalu di atas** bisa di-toggle (klik kanan area kosong)
- Posisi, ukuran, isi, dan pengaturan tersimpan otomatis di `settings.ini`
- **Tanpa install apa-apa** — pakai .NET Framework yang sudah ada di semua Windows 10/11

## Cara pakai

1. Unduh / clone repo ini
2. Double-click `StickyCountdown.exe`
3. Ketik nama model di barisnya
4. Klik angka countdown (atau klik kanan baris) → pilih **Hitung mundur ... jam** atau **Sampai jam ...**
5. Tambah baris dengan tombol **+ Tambah model**

Saat countdown habis: baris berubah hijau + tombol **Done** muncul. Setelah klik **Done**, baris kembali kosong dan widget bisa diperkecil lagi.

Kontrol lain:

- **−** (kanan atas) — perkecil jadi pill; **□** — buka lagi
- **×** (kanan atas) — keluar
- **Drag header** — pindahkan widget
- **Seret tepi / sudut kanan bawah** — ubah ukuran widget
- **Klik kanan area kosong** — menu: tambah model, perkecil, selalu di atas, keluar

> Catatan: saat ada aplikasi lain dalam mode fullscreen, Windows otomatis menyembunyikan window "selalu di atas" (perilaku sistem, bukan bug).

## Build dari source

Butuh Windows + .NET Framework 4.x (bawaan sistem). TIDAK perlu install Visual Studio atau .NET SDK.

```powershell
# dari folder project
powershell -ExecutionPolicy Bypass -File build.ps1
```

Atau manual dengan compiler bawaan Windows:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:StickyCountdown.exe `
  /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.VisualBasic.dll `
  /win32icon:app.ico src\StickyCountdown.cs
```

## Struktur project

```
StickyCountdown.exe   widget siap pakai
src\                  source code (C# WinForms, single file)
build.ps1             script build
tools\make-icon.ps1   regenerasi app.ico
app.ico               ikon aplikasi
screenshot.png        screenshot untuk README
```

## Lisensi

[MIT](LICENSE) © 2026 fannndi
