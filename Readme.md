# TUBES_GATOT - Robocode Tank Royale

## 1. Penjelasan Singkat Algoritma Greedy

Algoritma Greedy adalah algoritma yang mengambil keputusan terbaik pada kondisi saat ini. Pada Robocode Tank Royale, algoritma greedy digunakan untuk menentukan movement, target, radar, dan strategi menembak agar bot dapat memperoleh skor setinggi mungkin.

## 2. Bot yang Dibuat

### 2.1 MinimumDangerBot

**MinimumDangerBot** adalah bot utama yang menggunakan strategi **Greedy Minimum Danger**.

Bot ini memilih posisi dengan nilai bahaya paling kecil. Nilai bahaya dihitung berdasarkan posisi musuh, lintasan peluru virtual, jarak terhadap dinding, dan posisi pojok arena. Strategi ini bertujuan agar bot dapat bertahan lebih lama dan menghindari serangan lawan.

Lokasi bot:

```text
src/mainbot/minimumdangerbot/
```

### 2.2 AegisGreedy

**AegisGreedy** menggunakan strategi **Greedy Hybrid Target Scoring**.

Bot ini memilih target berdasarkan jarak musuh, energi musuh, kesegaran data scan, stabilitas target, dan jarak efektif tembakan. Strategi ini bertujuan memilih musuh yang paling menguntungkan untuk diserang.

Lokasi bot:

```text
src/alternatifbot/AegisGreedy/
```

### 2.3 LockBot

**LockBot** menggunakan strategi **Greedy Candidate Position Scoring**.

Bot ini mengevaluasi beberapa kandidat posisi, kemudian memilih posisi dengan skor terbaik. Skor dihitung berdasarkan jarak ideal terhadap musuh, gerakan lateral, jarak terhadap dinding, risiko pojok, dan kondisi energi.

Lokasi bot:

```text
src/alternatifbot/lockbot/
```

### 2.4 RangeKeeperGreedy

**RangeKeeperGreedy** menggunakan strategi **Greedy Ideal Distance**.

Bot ini menjaga jarak ideal terhadap musuh. Jika musuh terlalu jauh, bot akan mendekat. Jika musuh terlalu dekat, bot akan menjauh. Jika jarak sudah ideal, bot akan bergerak menyamping untuk menjaga posisi tembak.

Lokasi bot:

```text
src/alternatifbot/RangeKeeperGreedy/
```

## 3. Requirement Program

Program membutuhkan:

1. Robocode Tank Royale sesuai starter pack tugas besar.
2. .NET SDK.
3. Bahasa pemrograman C#.
4. File konfigurasi bot dengan format `.json`.
5. Visual Studio Code, Visual Studio, atau editor lain yang mendukung C#.

Setiap folder bot minimal berisi file:

```text
nama_bot.cs
nama_bot.csproj
nama_bot.json
```

Contoh pada bot utama:

```text
minimumdangerbot.cs
minimumdangerbot.csproj
minimumdangerbot.json
```

## 4. Cara Build Program

### Build Bot Utama

Masuk ke folder bot utama:

```bash
cd src/mainbot/minimumdangerbot
```

Jalankan build:

```bash
dotnet build
```

### Build Bot Alternatif

Build AegisGreedy:

```bash
cd src/alternatifbot/AegisGreedy
dotnet build
```

Build LockBot:

```bash
cd src/alternatifbot/lockbot
dotnet build
```

Build RangeKeeperGreedy:

```bash
cd src/alternatifbot/RangeKeeperGreedy
dotnet build
```

Jika build berhasil, hasil kompilasi akan berada pada folder:

```text
bin/Debug/
```

atau:

```text
bin/Release/
```

## 5. Cara Menjalankan Bot

Langkah menjalankan bot:

1. Jalankan Robocode Tank Royale.
2. Tambahkan folder bot ke daftar bot pada game engine.
3. Pilih bot yang ingin dijalankan.
4. Jalankan pertandingan.
5. Catat hasil skor, survival, bullet damage, dan peringkat akhir.

Bot utama yang digunakan untuk pengujian dan kompetisi adalah:

```text
MinimumDangerBot
```

## 6. Kendala Saat Development

Beberapa kendala yang dihadapi saat pengembangan adalah:

1. Bot terkadang sulit mempertahankan target ketika musuh bergerak cepat.
2. Prediksi tembakan tidak selalu akurat terhadap musuh yang bergerak acak.
3. Bot dapat kehilangan peluang damage jika terlalu fokus pada posisi aman.
4. Bot perlu menghindari dinding dan pojok agar tidak stuck.
5. Parameter danger perlu diuji beberapa kali agar movement tetap stabil.
6. Strategi yang baik pada mode 1 vs 1 belum tentu optimal pada kondisi banyak bot.


## 7. Struktur Folder Project

Pada halaman pertama, struktur utama repository ditampilkan ringkas sebagai berikut:

```text
README.md
Doc/
src/
├─ alternatifbot/
│  ├─ AegisGreedy/
│  │  ├─ AegisGreedy.cmd
│  │  ├─ AegisGreedy.cs
│  │  ├─ AegisGreedy.csproj
│  │  ├─ AegisGreedy.json
│  │  └─ AegisGreedy.sh
│  ├─ lockbot/
│  │  ├─ lockbot.cmd
│  │  ├─ lockbot.cs
│  │  ├─ lockbot.csproj
│  │  ├─ lockbot.json
│  │  └─ lockbot.sh
│  └─ RangeKeeperGreedy/
│     ├─ RangeKeeperGreedy.cmd
│     ├─ RangeKeeperGreedy.cs
│     ├─ RangeKeeperGreedy.csproj
│     ├─ RangeKeeperGreedy.json
│     └─ RangeKeeperGreedy.sh
└─ mainbot/
	 └─ minimumdangerbot/
			├─ minimumdangerbot.cmd
			├─ minimumdangerbot.cs
			├─ minimumdangerbot.csproj
			├─ minimumdangerbot.json
			└─ minimumdangerbot.sh
```

## 8. Author

**Kelompok 9 - GATOT**

1. Hafidz Haqiqi - 124140016
2. Pray Febry Valentine SG. - 124140184
3. Muhamad Rofik A. - 124140010

