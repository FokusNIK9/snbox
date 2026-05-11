# Пошаговая настройка сцены и префаба (S&box Scene System)

---

## ЧАСТЬ 1: НАСТРОЙКА СЦЕНЫ

### 1.1 — Объект «Game Controller» (управление игрой)

1. В **Scene** панели нажми **ПКМ → Create Empty**
2. Переименуй его в `Game Controller`
3. Позиция: `0, 0, 0` (оставь по умолчанию)
4. В **Inspector** нажми **Add Component** и добавь:
   - `GameNetworkManager`

> **Deprecated / superseded by MODULAR_ARCHITECTURE.md:** `GameNetworkManager` должен отвечать только за lobby, network setup и player spawning. Не добавляй в него волны, экономику, башни, enemies или UI.

5. В свойствах `GameNetworkManager`:
   - **Max Players** → `20` (или сколько нужно)
   - **Is Public** → ✓ (галочка)
   - **Lobby Name** → `TD Game`
   - **Auto Create Lobby** → ✓
   - **Player Prefab** → пока пусто (привяжем после создания префаба)
   - **Spawn Points** → пока пусто (добавим ниже)
   - **Spawn Random Radius** → `50`

### 1.2 — Объект «Camera» (камера)

1. В **Scene** панели нажми **ПКМ → Create Empty**
2. Переименуй его в `Camera`
3. Позиция: `0, 0, 600` (высота по умолчанию)
4. В **Inspector** нажми **Add Component** и добавь:
   - `CameraComponent` (движковый компонент камеры)
   - `TDCameraController` (наш скрипт)
5. В свойствах `TDCameraController`:
   - **Height** → `600`
   - **Pitch Angle** → `60`
   - **Follow Speed** → `8`
   - **Allow Zoom** → ✓
   - **Min Height** → `300`
   - **Max Height** → `1200`
   - **Zoom Step** → `50`
   - **Zoom Smoothing** → `6`
   - **Allow Cursor Offset** → ✓
   - **Cursor Influence** → `0.15`
   - **Max Cursor Offset** → `150`

### 1.3 — Объект «SpawnPoint» (точка спавна)

1. В **Scene** панели нажми **ПКМ → Create Empty**
2. Переименуй его в `SpawnPoint`
3. Позиция: `0, 0, 0` (или где хочешь, чтобы игроки появлялись)
4. Компоненты добавлять НЕ нужно — это просто маркер позиции
5. Можешь создать несколько `SpawnPoint2`, `SpawnPoint3` и т.д. в разных местах

### 1.4 — Привязка SpawnPoints к GameNetworkManager

1. Выбери объект `Game Controller`
2. В свойствах `GameNetworkManager` → **Spawn Points**
3. Нажми `+` и перетащи объект `SpawnPoint` из Scene
4. Повтори для каждого спавн-поинта

### 1.5 — Пол / Карта

Если у тебя ещё нет пола:
1. **ПКМ → 3D Object → Plane** (или используй свою карту)
2. Масштаб: `100, 100, 1` (большая плоскость)
3. Позиция: `0, 0, 0`
4. Убедись, что на полу есть **Collider** (BoxCollider или MeshCollider) — чтобы Raycast курсора попадал в пол

---

## ЧАСТЬ 2: СОЗДАНИЕ ПРЕФАБА ИГРОКА (player.prefab)

### 2.1 — Корень префаба

1. В **Scene** панели нажми **ПКМ → Create Empty**
2. Переименуй его в `Player`
3. Позиция: `0, 0, 0`
4. В **Inspector** нажми **Add Component** и добавь по очереди:
   - `CharacterController` (движковый — физика передвижения)
   - `PlayerUnitController` (наш скрипт)
   - `PlayerCargo` (наш скрипт — система груза)
   - `UnitHealth` (наш скрипт — здоровье)
   - `UnitNameTag` (наш скрипт — имя над головой)

> **Deprecated / superseded by MODULAR_ARCHITECTURE.md:** универсальный монолитный `UnitHealth` больше не является целевой архитектурой. Новый подход: базовый `HealthComponent` для HP/damage/death, а respawn, скрытие модели, уничтожение, награды и visual health — отдельные компоненты.

5. В свойствах `PlayerUnitController`:
   - **Controller** → оставь пусто (найдёт сам через `Components.Get`)
   - **Animation Helper** → оставь пусто (найдёт сам)
   - **Move Speed** → `300`
6. В свойствах `PlayerCargo`:
   - **Cargo Count** → `0` (по умолчанию)
7. В свойствах `UnitHealth`:
   - **Max Health** → `100`
   - **Allow Respawn** → ✓
   - **Respawn Delay** → `3`
   - **Hide On Death** → ✓

### 2.2 — Дочерний объект: Модель персонажа (Citizen)

1. В иерархии **ПКМ на `Player` → Create Empty** (дочерний)
2. Переименуй в `Body`
3. В **Inspector** добавь:
   - `SkinnedModelRenderer` → модель: `models/citizen/citizen.vmdl` (стандартный Citizen)
   - `CitizenAnimationHelper`
4. В свойствах `CitizenAnimationHelper`:
   - **Target** → перетащи сюда компонент `SkinnedModelRenderer` с этого же объекта `Body`

### 2.3 — Дочерний объект: TargetCursor (курсор прицела)

> **ВАЖНО:** Это ОТДЕЛЬНЫЙ дочерний объект внутри `Player`, НЕ компонент на самом `Player`!

1. В иерархии **ПКМ на `Player` → Create Empty** (дочерний)
2. Переименуй в `TargetCursor`
3. **Scale (масштаб):** `0.3, 0.3, 0.3`
4. В **Inspector** добавь:
   - `ModelRenderer` → модель: `models/dev/sphere.vmdl`
5. Теперь добавь компонент:
   - `TargetCursor` (наш скрипт)
6. В свойствах `TargetCursor`:
   - **Cursor Renderer** → перетащи сюда компонент `ModelRenderer` с этого же объекта `TargetCursor`
   - **Normal Color** → Белый (по умолчанию)
   - **Hover Color** → Зелёный
   - **Normal Scale** → `1, 1, 1`
   - **Hover Scale** → `1.5, 1.5, 1.5`

### 2.4 — Дочерний объект: InteractionPoint (рюкзак / точка взаимодействия)

> Это объект "за спиной" игрока — в него целятся другие игроки чтобы украсть груз.

1. В иерархии **ПКМ на `Player` → Create Empty** (дочерний)
2. Переименуй в `InteractionPoint`
3. **Позиция:** `0, -15, 40` (за спиной и чуть выше центра — подбери визуально)
4. В **Inspector** добавь:
   - `BoxCollider`
     - **Is Trigger** → **ВЫКЛЮЧИ** (false / без галочки) — чтобы Raycast курсора попадал в него
     - **Scale/Size** → `30, 30, 30` (подбери по размеру)
   - `ModelRenderer` → модель: `models/dev/box.vmdl` (дебаг-визуал рюкзака, потом заменишь на нормальную модель)
     - **Scale** модели подбери чтобы куб был размером с рюкзак
   - `InteractableObject` (наш скрипт)
     - **Interaction Time** → `1.5` (секунд нужно держать ЛКМ)
     - **Is Active** → ✓

### 2.5 — Дочерний объект внутри InteractionPoint: Text (счётчик груза)

1. В иерархии **ПКМ на `InteractionPoint` → Create Empty** (дочерний внутри InteractionPoint)
2. Переименуй в `Text`
3. **Позиция:** `0, 0, 25` (над рюкзаком)
4. В **Inspector** добавь:
   - `TextRenderer` (движковый компонент для 3D текста)
     - **Text** → `0` (начальное значение, скрипт перезапишет)
     - **Font Size** → `64` (подбери)
     - **Color** → Белый
   - `CargoTextDisplay` (наш скрипт)
     - Ничего привязывать НЕ нужно — скрипт сам найдёт `TextRenderer` и `PlayerCargo` через `GetInAncestorsOrSelf`

### 2.6 — Сохраняем как префаб

1. **Перетащи объект `Player` из Scene в папку Assets** (в Project панели)
2. Это создаст файл `player.prefab` (или `Player.prefab`)
3. **Удали объект `Player` из сцены** — он будет спавниться через GameNetworkManager

### 2.7 — Привязка префаба к GameNetworkManager

1. Выбери объект `Game Controller` на сцене
2. В свойствах `GameNetworkManager` → **Player Prefab**
3. Перетащи файл `player.prefab` из Assets сюда

---

## ЧАСТЬ 3: ИТОГОВАЯ ИЕРАРХИЯ

### Сцена:
```
Scene
├── Game Controller          ← GameNetworkManager
├── Camera                   ← CameraComponent + TDCameraController
├── SpawnPoint               ← пустой маркер
├── Floor / Map              ← Collider обязателен!
```

### Префаб Player:
```
Player                       ← CharacterController
                              + PlayerUnitController
                              + PlayerCargo
                              + UnitHealth
                              + UnitNameTag
│
├── Body                     ← SkinnedModelRenderer (citizen.vmdl)
│                             + CitizenAnimationHelper
│
├── TargetCursor             ← ModelRenderer (sphere.vmdl), scale 0.3
│                             + TargetCursor (скрипт)
│
└── InteractionPoint         ← BoxCollider (IsTrigger OFF!)
                              + ModelRenderer (box.vmdl)
                              + InteractableObject
    │
    └── Text                 ← TextRenderer
                              + CargoTextDisplay
```

---

## ЧАСТЬ 4: ЧЕКЛИСТ ПЕРЕД ЗАПУСКОМ

- [ ] На сцене есть `Game Controller` с `GameNetworkManager`
- [ ] `Player Prefab` привязан в GameNetworkManager
- [ ] Хотя бы один `SpawnPoint` добавлен в список
- [ ] На сцене есть `Camera` с `CameraComponent` + `TDCameraController`
- [ ] На полу / карте есть Collider (чтобы курсор работал)
- [ ] Префаб `Player` содержит `CharacterController`
- [ ] Дочерний `TargetCursor` имеет `ModelRenderer` и привязку `Cursor Renderer`
- [ ] `InteractionPoint` → `BoxCollider` → **Is Trigger = FALSE**
- [ ] Дочерний `Text` внутри `InteractionPoint` имеет `TextRenderer` + `CargoTextDisplay`
- [ ] Объект `Player` удалён со сцены (спавнится через сеть)

---

## ЧАСТЬ 5: КАК ТЕСТИРОВАТЬ

1. Нажми **Play** в редакторе
2. Ты появишься на спавн-поинте (хост получит 5 коробок автоматически)
3. Двигайся на **WASD**
4. Зум — **колёсико мыши**
5. Наведи курсор на `InteractionPoint` (рюкзак) другого игрока — курсор позеленеет
6. **Зажми ЛКМ на 1.5 секунды** — украдёшь 1 коробку
7. Для теста мультиплеера: иконка сети → `Join via new instance`
