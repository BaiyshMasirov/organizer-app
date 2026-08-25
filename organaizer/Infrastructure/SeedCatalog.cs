namespace organaizer.Infrastructure;

public static class SeedCatalog
{
    public static readonly (string Code,string Name,string Symbol,int Precision)[] Currencies =
    [
        ("AED","Дирхам ОАЭ","د.إ",2), ("CNY","Китайский юань","¥",2), ("EUR","Евро","€",2),
        ("KGS","Кыргызский сом","с",2), ("RUB","Российский рубль","₽",2),
        ("USD","Доллар США","$",2), ("USDT","Tether","₮",8)
    ];

    public static readonly string[] BrokerClients =
    [
        "A&A", "A&A Oil and Gas Limited", "AROIL Limited", "COMMODITIES", "GOLD ENTERPRISE LOJISTIK VE DIS TICARET LIMITED SIRKETI",
        "Modul Bank (Plutus)", "Naumov Igor (Navumau Ihar)", "SP Oil", "Акварро ЛТД", "Аргос Трейдинг", "Велиюлин Эдгар",
        "Кантаев Арби", "Кантаев Магомед", "Катри Петролеум", "Когай Вадим Савельевич", "Конаплев Тимофей", "Молчанов Д.В.",
        "ОАО EVDE", "ООО Пиксель Интернет", "Ориент Кэпитал", "ОсОО NTS Tehnosystem", "ОсОО Амано", "ОсОО Венто Групп",
        "ОсОО Криптекс", "ОсОО МЕКС", "ОсОО НТС Техносистемс", "ОсОО Сафи Петролеум", "ОсОО Тикетнет", "ОсОО Петрогаз Трейдинг",
        "ОсОО Биткеш", "ОсОО М-Трейд", "ОсОО Октан", "Петрорусс", "Петрорусс ДМСС", "Роял Пюр Голд", "Слотин А.Г.",
        "TORIN", "Торопов Сергей", "Транс Глобал", "Шушарин Роман"
    ];

    public static readonly string[] LiquidityClients =
    [
        "A ONE UNION", "A&A ТРЕЙДИНГ", "ARP DIGITAL", "Amir", "BAKAI BANK", "BINANCE", "B_360", "BitRuby", "COMMODITIES",
        "CRAIT", "Coinex", "Delta DA", "EFS DMCC", "ENROUTE (АНБ)", "FU YING GUO", "FUZE", "Fasset", "IMEX", "ITsdv Limited",
        "JAVAS", "KAGAN PAY", "KAPEX", "KRAKEN", "Kadam Company", "MALAKHIT Trading Limited", "MBIO", "MEX Digital FZE",
        "MIDAS OIL (ФЕНЯ)", "MTSL", "MULTIBANK", "Man & Money (M&M)", "Meruti", "Money Broker", "NETEX", "NUVOS",
        "PANASIA (Commodities)", "Quantum", "ROSIND CORP LLP", "Royal Pure Gold", "SIRIUS", "SP Oil", "Satoshi Trading",
        "Sun Energy", "TORIN", "Ай Ти Компания (Шашков)", "Акварро ЛТД", "Альт Спот", "Амир Марат", "Аргос Трейдинг",
        "Артур (Ориент)", "Асан Хамзин", "Венто Групп", "Голд Энтерпрайз", "ИК Ориент Капитал", "Интелион", "Каган Пэй",
        "Катри Петролеум", "НОРО Ворлд", "ООО Эксес ГРУП", "ОРИЕНТ (СЛОТИН)", "Октан", "Ориент Кэпитал",
        "ОсОО Айти Компания", "ОсОО Интер Кит", "ОсОО Сатоши", "ОсОО Эксес Групп", "Петрогаз Трейдинг", "Петрорусс ДМСС",
        "Площадка СТ", "Роял Пюр Голд", "СRAIT DMCC", "Collect Exchange", "Сатоши Трейдинг", "Сафи Петролеум",
        "ТОКЕНСПОТ", "Торин", "Трейдинг (Альфа Ойл)", "Эксес Груп"
    ];

    public static readonly (string Name, Domain.InstitutionKind Kind)[] Institutions =
    [
        ("A&A Vexel",Domain.InstitutionKind.PaymentSystem), ("BAKAI",Domain.InstitutionKind.Bank),
        ("BAKAI AED",Domain.InstitutionKind.Bank), ("BAKAI USD",Domain.InstitutionKind.Bank),
        ("ОТП",Domain.InstitutionKind.Bank), ("ВТБ",Domain.InstitutionKind.Bank), ("РНКО",Domain.InstitutionKind.Bank),
        ("НОДА",Domain.InstitutionKind.Bank), ("Айыл Банк",Domain.InstitutionKind.Bank), ("H&H",Domain.InstitutionKind.Bank),
        ("INVESTBANK",Domain.InstitutionKind.Bank), ("KKB",Domain.InstitutionKind.Bank), ("CBD",Domain.InstitutionKind.Bank),
        ("Альфа",Domain.InstitutionKind.Bank), ("Совкомбанк",Domain.InstitutionKind.Bank), ("ТелеПорт Банк",Domain.InstitutionKind.Bank),
        ("Унифондбанк",Domain.InstitutionKind.Bank), ("МТИ",Domain.InstitutionKind.Bank), ("МБА",Domain.InstitutionKind.Bank),
        ("Vexel",Domain.InstitutionKind.PaymentSystem), ("KRAKEN",Domain.InstitutionKind.Exchange),
        ("BINANCE",Domain.InstitutionKind.Exchange), ("MULTIBANK",Domain.InstitutionKind.Exchange),
        ("MEX Digital",Domain.InstitutionKind.Exchange), ("Ledger",Domain.InstitutionKind.Wallet),
        ("Coinex",Domain.InstitutionKind.Exchange), ("Fasset",Domain.InstitutionKind.Exchange)
    ];
}
