# 📖 Game Design Document (GDD)
## Project Codename: **ECHOES OF THE LOST**

> **Version:** 0.1 (Draft) | **Date:** 2026-10-23 | **Author:** Solo Dev

---

## 1. Overview

| Field | Value |
|---|---|
| **Title** | Echoes of the Lost (Working Title) |
| **Genre** | Open-World Action Adventure / Archaeological Exploration |
| **Platform** | PC (Primary), Console (Secondary) |
| **Engine** | Unity 6 LTS |
| **Render Pipeline** | URP — Stylized (Genshin / Zelda BotW aesthetic) |
| **Camera** | Third-Person 3D |
| **Target Rating** | T (Teen) / PEGI 12 |
| **Development Stage** | Pre-Production |

---

## 2. Elevator Pitch

> *"Khám phá những nền văn minh bị lãng quên trên một lục địa đầy bí ẩn — giải mã cổ vật, leo trèo tàn tích, đối mặt với những thực thể bảo vệ di sản của người xưa, và dần dần lắp ghép sự thật về một thảm họa đã xóa sổ nền văn minh đó."*

---

## 3. Core Pillars

| # | Pillar | Mô tả |
|---|---|---|
| 1 | **Khám Phá Tự Do** | Player có thể leo, bơi, trèo gần như mọi địa hình. Thế giới thưởng cho sự tò mò. |
| 2 | **Bí Ẩn & Khám Phá** | Mỗi tàn tích là một câu đố về lịch sử. Lore ẩn trong môi trường, cổ vật, nhật ký. |
| 3 | **Chiến Đấu Năng Động** | Kẻ thù đa dạng với AI thông minh. Dodge-parry-combo. Không grind — chất lượng hơn số lượng. |
| 4 | **Thế Giới Sống** | Day/Night, Weather, NPC schedules, World Events tạo cảm giác thế giới tồn tại độc lập. |

---

## 4. Core Gameplay Loop

```
MACRO LOOP (Per Playthrough):
Khám phá khu vực mới → Phát hiện tàn tích → Giải mã bí ẩn → 
Đối đầu Boss Khu Vực → Mở khóa lore mới → Tiếp tục khám phá

MICRO LOOP (Per Session 15-30 phút):
Đến POI → Khám phá địa hình → Thu thập manh mối/cổ vật →
Giải puzzle / Chiến đấu → Nhận reward (loot + lore) → 
Level up / Unlock ability → Đặt mục tiêu tiếp theo

META LOOP (Long-term):
Skill tree progression → Unlock vùng đất mới →
Hoàn thành faction quests → Đa kết thúc dựa theo lựa chọn
```

---

## 5. Thế Giới: Lục Địa AETHARA

### 5.1 Lore Cơ Bản
- **Aethara** là lục địa từng là nơi phát triển của **Nền Văn Minh Solenne** — một nền văn minh hội tụ khoa học và linh năng.
- **1000 năm trước:** Sự kiện "The Unraveling" đã xóa sổ phần lớn dân số và gần như xóa sạch mọi dấu vết.
- **Hiện tại:** Các quốc gia mới hình thành trên tàn tích, nhưng không ai hiểu Solenne đã làm gì.
- **Player** là nhà thám hiểm/khảo cổ học vô tình trở thành chìa khóa giải mã bí ẩn.

### 5.2 Regions (5 khu vực chính)

| Region | Địa hình | Nét đặc trưng | Cấp độ gợi ý |
|---|---|---|---|
| **Verda Wilds** | Rừng xanh mướt, đồng cỏ | Starting area, tàn tích nhỏ, tutorial tự nhiên | 1–10 |
| **The Ashen Reach** | Đồng bằng tro bụi, miệng núi lửa đã tắt | Nơi The Unraveling xảy ra đầu tiên, lore dày đặc | 15–25 |
| **Crystalis Peaks** | Núi cao, hang tinh thể phát sáng | Kho báu Solenne, puzzle khó nhất | 20–30 |
| **Mirewood Bogs** | Đầm lầy, sương mù dày | Bí ẩn tối nhất, faction nguy hiểm | 18–28 |
| **Solenne Citadel** | Thành phố Solenne bán bị phá hủy | Endgame, final revelation | 30+ |

---

## 6. Nhân Vật Chính (Protagonist)

| Field | Value |
|---|---|
| **Tên mặc định** | Kael (customizable) |
| **Giới tính** | Lựa chọn |
| **Background** | Nhà thám hiểm tự do, từng là trợ lý trong viện khảo cổ |
| **Motivation ban đầu** | Tìm kiếm di tích Solenne để trả nợ / chứng minh bản thân |
| **Motivation thực sự** | Khám phá sự thật về cha/mẹ và mối liên hệ với The Unraveling |
| **Personality** | Tò mò, khôi hài, kiên cường nhưng ẩn nỗi sợ hãi |

---

## 7. Factions (3 phe phái chính)

| Faction | Mô tả | Quan hệ với Player |
|---|---|---|
| **The Accord** | Tổ chức khảo cổ chính thống, coi Solenne artifacts là tài sản nhà nước | Ally ban đầu, có thể xung đột |
| **Void Seekers** | Giáo phái muốn tái kích hoạt công nghệ Solenne bằng mọi giá | Antagonist chính |
| **The Drifters** | Cộng đồng nomad sống trong tàn tích, coi đó là thánh địa | Neutral → Ally nếu player giúp đỡ |

---

## 8. Progression System

```
Level 1-10:  Verda Wilds — Học cơ bản, khám phá tự do
Level 10-20: Mở thêm regions, tăng combat depth
Level 20-30: Endgame content, Solenne tech integration
Level 30:    Prestige / New Game+ mode
```

**Skill Trees:**
- 🗡️ **Combat:** Combo chains, parry windows, elemental infusion
- 🏃 **Traversal:** Grappling hook, glide, enhanced climbing
- 🔬 **Scholar:** Faster artifact analysis, hidden lore, better prices

---

## 9. Monetization

- **Model:** Buy-to-Play (mua đứt một lần)
- **DLC:** Story expansions (optional, not pay-to-win)
- **No:** Battle pass, loot boxes, energy systems

---

## 10. Reference Games

| Game | Học gì |
|---|---|
| Zelda: Breath of the Wild | Traversal freedom, world density, puzzle design |
| Genshin Impact | Visual style, elemental system, world events |
| Assassin's Creed Origins | Archaeological lore integration, POI design |
| Outer Wilds | Narrative through exploration, mystery design |
| Horizon Zero Dawn | Lore reveal pacing, robot-like enemy design |

---

## 11. Cốt Truyện — Outline Sơ Bộ

```
ACT 1 (Verda Wilds):
- Kael đến Aethara theo thông tin từ cha/mẹ đã mất tích
- Gặp The Accord, nhận nhiệm vụ đầu tiên
- Khám phá tàn tích đầu tiên → kích hoạt "Echo" (ký ức holographic của Solenne)
- Gặp Void Seekers lần đầu → họ muốn cùng Echo đó

ACT 2 (Ashen Reach + Crystalis + Mirewood):
- Chạy đua với Void Seekers tìm các Solenne Cores
- Khám phá The Unraveling là do chính Solenne tạo ra (self-destruction)
- Twist: Cha/mẹ của Kael là thành viên cũ của Void Seekers
- Lựa chọn: Hợp tác với The Accord, Drifters hay Void Seekers?

ACT 3 (Solenne Citadel):
- Final revelation: The Unraveling là hệ thống tự bảo vệ, không phải thảm họa
- 3 Endings dựa vào faction reputation & lựa chọn
  → Ending A: Khai thác Solenne tech (Accord) — Power but risk
  → Ending B: Đóng băng tất cả (Drifters) — Safety but stagnation  
  → Ending C: Giải phóng hoàn toàn (Void) — Chaotic rebirth
```

---

*Document này sẽ được cập nhật liên tục theo tiến độ development.*
