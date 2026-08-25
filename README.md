# Finance Organizer

Управленческий учет двух компаний: брокера и поставщика ликвидности. Стек: ASP.NET Core 8 Razor Pages, CQRS, EF Core, PostgreSQL.

## Запуск

```powershell
docker compose up --build
```

Открыть http://localhost:5050. Пароль БД для production задается переменной `POSTGRES_PASSWORD`.

## Миграции

При старте приложение применяет ожидающие EF Core migrations через `Database.MigrateAsync()`. Начальная миграция лежит в `organaizer/Infrastructure/Migrations`.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MigrationName --project organaizer/organaizer.csproj --startup-project organaizer/organaizer.csproj --output-dir Infrastructure/Migrations
dotnet tool run dotnet-ef database update --project organaizer/organaizer.csproj --startup-project organaizer/organaizer.csproj
```

Интерфейс основан на бесплатном MIT-licensed Tabler, версия зафиксирована на 1.4.0.

## Тестовый вход

- Логин: `admin`
- Пароль: `inFO@)20`

Пользователь и роль `Administrator` создаются идемпотентно при первом старте. Для production тестовый пароль необходимо заменить.

## Справочники и отчетность

- Справочник валют: AED, CNY, EUR, KGS, RUB, USD, USDT; валюты выбираются из выпадающих списков.
- Справочник клиентов заполняется идемпотентно из исходных таблиц Orient Capital и A&A и поддерживает ручное добавление/редактирование.
- Промежуточный месячный отчет показывает итоги по типам операций, валютные потоки, расходы и график прибыли.
- Seed добавляет только отсутствующие записи. При обновлении `docker compose up -d --build` существующий PostgreSQL volume не очищается.
- Исторический импорт из Excel хранит ключ источника, лист, номер строки и исходный JSON. Повторный запуск безопасен и не создает дубликаты.

Сделка хранит две валютные стороны, комиссию, прибыль в базовой валюте и срок расчета. Фактические движения (`Settlement`) могут поступать частями и в разные дни; знак суммы означает приход или расход по счету. Остаток счета равен начальному остатку плюс все движения. Статус сделки закрывается после покрытия обеих сторон.

CQRS реализован отдельными командами и запросами в `Application/Cqrs.cs`; запись и чтение не смешиваются в PageModel.
