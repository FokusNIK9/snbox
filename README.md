# snbox — TD Multiplayer Game (s&box)

Казуальная мультиплеерная Tower Defense игра для [s&box](https://sbox.game).

## Структура

```
Code/
  Components/
    GameNetworkManager.cs    — Лобби, подключения, спавн игроков
    PlayerUnitController.cs  — WASD управление юнитом (top-down)
    UnitHealth.cs            — HP, урон, смерть, респавн
    TDCameraController.cs    — Top-down камера с зумом
    UnitNameTag.cs           — Имя игрока над юнитом
    UnitAnimSync.cs          — Синхронизация анимаций по сети
```

## Как использовать

1. Создайте новый проект в s&box Editor
2. Скопируйте папку `Code/` в свой проект
3. Соберите **префаб юнита** (см. `ARCHITECTURE.md`)
4. Настройте **сцену** с `GameNetworkManager` и камерой

Подробная документация: [`ARCHITECTURE.md`](./ARCHITECTURE.md)

## Требования

- s&box (актуальная версия)
- ~20 игроков в лобби
- Приоритет: плавность, отсутствие телепортации, модульность
