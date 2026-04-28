# TD Game — Модульная система компонентов для s&box

## Архитектура

Все компоненты **полностью самодостаточны** и **не конфликтуют** друг с другом.
Написаны строго по официальной документации s&box (`sbox.game/dev/doc`, `sbox.game/api`).

Принцип: кинул на объект → настроил в инспекторе → работает.

---

## Компоненты

### 1. `GameNetworkManager` — Сетевой менеджер
**Куда:** На отдельный пустой GameObject в сцене (один на сцену).

| Параметр | Описание | По умолчанию |
|---|---|---|
| MaxPlayers | Макс. игроков в лобби | 20 |
| Privacy | Приватность лобби | Public |
| LobbyName | Название лобби | "TD Game" |
| AutoCreateLobby | Авто-создание при старте | true |
| PlayerPrefab | Префаб юнита игрока | — |
| SpawnPoints | Точки спавна (round-robin) | [] |
| SpawnRandomRadius | Случайный разброс спавна | 50 |

**Что делает:**
- Создает лобби при старте сцены (`Networking.CreateLobby`)
- Реализует `Component.INetworkListener` — спавнит `PlayerPrefab` для каждого подключившегося
- Назначает ownership через `NetworkSpawn(connection)`
- `OnValidate` для защиты значений в инспекторе

---

### 2. `PlayerUnitController` — Управление юнитом
**Куда:** На GameObject юнита (префаб игрока).

| Параметр | Описание | По умолчанию |
|---|---|---|
| MoveSpeed | Скорость движения | 200 |
| RotationSmoothing | Плавность поворота | 10 |
| Friction | Трение (торможение) | 6 |
| Acceleration | Ускорение | 10 |
| GravityStrength | Сила гравитации | 800 |

**Что делает:**
- `Input.AnalogMove` (WASD) для top-down управления
- `[RequireComponent]` автоматически создает `CharacterController`
- `CharacterController.Accelerate()`, `ApplyFriction()`, `Move()` — по документации
- Плавный поворот через `Rotation.Slerp`
- `[Sync]` — WishDirection, IsMoving; `[Sync(SyncFlags.Interpolate)]` — CurrentSpeed
- Интерполяция трансформа встроена в s&box (без телепортации)

---

### 3. `UnitHealth` — Система здоровья
**Куда:** На GameObject юнита.

| Параметр | Описание | По умолчанию |
|---|---|---|
| MaxHealth | Максимальное HP | 100 |
| AllowRespawn | Разрешить респавн | true |
| RespawnDelay | Задержка респавна (сек) | 3 |
| RespawnPoint | Точка респавна | null (на месте) |
| HideOnDeath | Скрыть модель при смерти | true |

**Что делает:**
- `OnAwake` — инициализирует HP
- `ApplyDamage()` — автоматический `[Rpc.Owner]` если вызван с прокси
- Смерть: `Invoke(RespawnDelay, Respawn)` — встроенный таймер s&box
- `GameObject.Network.ClearInterpolation()` при телепорте на респавн
- Отключает `PlayerUnitController` + `ModelRenderer` при смерти
- `[Rpc.Broadcast]` хуки для VFX/SFX на всех клиентах
- События: `OnHealthChanged`, `OnDeath`, `OnRespawned`

---

### 4. `TDCameraController` — Top-Down камера
**Куда:** На отдельный GameObject с `CameraComponent`.

| Параметр | Описание | По умолчанию |
|---|---|---|
| Height | Высота камеры | 600 |
| BackOffset | Смещение назад | 200 |
| PitchAngle | Угол наклона (90=сверху) | 60° |
| FollowSpeed | Скорость следования | 8 |
| AllowZoom | Разрешить зум | true |
| MinHeight / MaxHeight | Диапазон зума | 300 / 1200 |
| ZoomStep | Шаг зума | 50 |
| ZoomSmoothing | Плавность зума | 6 |

**Что делает:**
- `Scene.GetAllComponents<PlayerUnitController>()` — находит локального юнита
- `IsValid()` проверка по документации s&box
- `Input.MouseWheel.y` для зума
- `LerpTo` для плавного следования и зума

---

### 5. `UnitNameTag` — Имя игрока
**Куда:** На GameObject юнита.

| Параметр | Описание | По умолчанию |
|---|---|---|
| HeightOffset | Высота текста | 80 |
| HideForOwner | Скрыть для владельца | false |

**Что делает:**
- `Component.INetworkSpawn` — получает `DisplayName` из `Connection` при спавне
- `[Sync]` для имени
- `DrawGizmos()` — рисует текст над юнитом

---

### 6. `UnitAnimSync` — Синхронизация анимаций
**Куда:** На GameObject юнита (рядом с `SkinnedModelRenderer`).

| Параметр | Описание | По умолчанию |
|---|---|---|
| TargetRenderer | SkinnedModelRenderer (авто-поиск) | null |
| SpeedParam | Параметр скорости | "move_speed" |
| GroundedParam | Параметр "на земле" | "grounded" |
| AliveParam | Параметр "жив" | "alive" |
| WishDirParam | Параметр направления | "wish_direction" |

**Что делает:**
- `OnAwake` — кеширует ссылки через `GetComponent<T>()` и `GetComponentInChildren<T>()`
- `[Sync(SyncFlags.Interpolate)]` для скорости — плавная анимация у всех клиентов
- Имена параметров настраиваемые — подходит под любой Animgraph
- `SkinnedModelRenderer.Set()` каждый кадр в `OnUpdate`

---

## Сборка префаба юнита

```
PlayerUnit (GameObject, NetworkMode = Object)
├── PlayerUnitController    ← управление (авто-создаёт CharacterController)
├── UnitHealth              ← здоровье
├── UnitNameTag             ← имя над головой
├── UnitAnimSync            ← синхронизация анимаций
├── SkinnedModelRenderer    ← 3D модель
└── (Colliders по необходимости)
```

## Сборка сцены

```
Scene
├── GameNetworkManager      ← на пустом объекте, PlayerPrefab = PlayerUnit
├── TDCamera (GameObject)
│   ├── CameraComponent
│   └── TDCameraController
├── SpawnPoint_1            ← пустые объекты
├── SpawnPoint_2
├── SpawnPoint_3
├── Ground / Map
└── (Другие объекты)
```

## Гарантии

1. **Нет конфликтов** — связи через `GetComponent<T>()` с null-проверками, `IsValid()` по документации
2. **Нет телепортации** — s&box автоматически интерполирует трансформы + `SyncFlags.Interpolate` для визуальных значений
3. **20+ игроков** — `[Sync]` отправляет данные только при изменении; минимальный трафик
4. **Каждый компонент можно убрать** — остальные продолжат работать
5. **`OnValidate`** — защита от некорректных значений в инспекторе
6. **Lifecycle** — `OnAwake` для кеширования, `OnFixedUpdate` для физики, `OnUpdate` для визуала
