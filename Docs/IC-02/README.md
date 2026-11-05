# IC-02 · Coin Forge

## Integrasi Unity

- `Assets/Scenes/Main.unity` memakai `Assets/Prefabs/CoinForgeUI.prefab`.
- `CoinForgeUI.cs` menangani event, aksi, harga, popup, dan statistik. `CoinForgeUI.View.cs` membangun tampilan dan menyesuaikan safe area. `CoinForgeUI.Tap.cs` menangani scale punch, flipbook, dan pool 20 label.
- `Assets/Editor/CoinForgeSetup.cs` memasang prefab yang sama saat membuat ulang Main.
- Panel upgrade dan shield popup berbagi batas bawah. Insets mengikuti safe area, tinggi banner runtime, dan jarak 16dp. Selagi tinggi runtime belum tersedia, acuan 167px diskalakan terhadap lebar 1080px.
- Panel memakai border 32px pada keempat sisi. Tombol memakai 48px kiri dan kanan serta 40px atas dan bawah. Keduanya menggunakan `Image.Type.Sliced`.
- UI memakai font runtime bawaan Unity dengan gaya tebal. Ukuran sentuh dan ukuran teks minimum mengikuti density dan scaled density Android.

## Slot IC-03

Nama objek Image mengikuti nama PNG. Batas impor adalah dimensi maksimum Android.

| Slot PNG | Sumber | Batas impor |
| --- | --- | --- |
| `bld_01_coin_press.png`, `bld_02_mini_forge.png`, `bld_03_smelter.png`, `bld_04_foundry.png`, `bld_05_mint.png`, `bld_06_vault.png`, `bld_07_bank.png`, `bld_08_exchange.png`, `bld_09_treasury.png`, `bld_10_galaxy_mint.png`, `bld_locked.png` | 512×512 | 256 |
| `coin_icon.png`, `gem_icon.png` | 128×128 | 128 |
| `coin_main.png` | 1024×1024 | 512 |
| `ic_ad_badge.png`, `ic_boost2x.png`, `ic_milestone.png`, `ic_offline.png`, `ic_prestige.png`, `ic_timewarp.png` | 256×256 | 256 |
| `bg_main.png` | 1080×2340 | 2048 |
| `panel_frame.png` | 512×512 | 512 |
| `btn_gold.png`, `btn_gray.png`, `btn_green.png` | 512×160 | 512 |
| `vfx_coin_burst_01.png` hingga `vfx_coin_burst_08.png` | 256×256 per frame | 256 |
| `vfx_sparkle_01.png` hingga `vfx_sparkle_06.png` | 256×256 per frame | 256 |

Aset toko yang diimpor terdiri dari `app_icon_512.png` (512×512) dan `feature_graphic.png` (1024×500).

## Catatan PM

- IC-01 belum menyimpan durasi main. Statistik mulai mengakumulasi waktu foreground sejak IC-02 dalam kunci PlayerPrefs UI tersendiri.
- IC-01 menyediakan tinggi banner melalui getter. UI membaca perubahan pada tick koin dan saat ukuran Canvas berubah.
- Durasi offline yang ditampilkan berasal dari `OfflineEarned` dan mengikuti batas waktu yang dihitung IC-01.
- Rumus harga, hadiah, pembelian, dan Prestige memakai API IC-01. Kode inti tidak berubah.

## Verifikasi

Unity 6000.3.27f1 berhasil membangun APK development Android. Play Mode lulus 18 pemeriksaan, termasuk harga dan ketersediaan x1, x10, dan Max, pembelian, Welcome Back dan klaim offline, Statistik, konfirmasi Prestige, serta tampilan dan nilai Time Warp sesuai kondisi siap dan batas 180 detik. Saat rewarded belum siap, tombol Boost nonaktif dengan label “Memuat…” dan Time Warp tersembunyi.

| Tangkapan layar Unity Editor | Resolusi | Kondisi |
| --- | --- | --- |
| [16:9](16x9.png) | 1080×1920 | Safe area penuh |
| [19.5:9](19.5x9.png) | 1080×2340 | Inset notch simulasi 84px |
| [20:9](20x9.png) | 1080×2400 | Inset punch-hole simulasi 72px |
| [Welcome Back dengan simulasi banner](welcome-banner-simulated.png) | 1080×2340 | Banner 167px, jarak terukur 44px (16dp pada 440dpi) |

Redmi Note 7 menolak pemasangan APK QA lewat USB dengan `INSTALL_FAILED_USER_RESTRICTED`. Banner AdMob native dan performa 60 FPS/GC saat tap cepat masih menunggu pengukuran di perangkat.
