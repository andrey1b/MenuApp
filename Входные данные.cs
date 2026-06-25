// Входные данные
decimal monthlyBudget = 10000;
int familyCount = 2;
int days = 30;

// 1. Расчёт дневного лимита
decimal dailyLimit = monthlyBudget / days;

// 2. Распределение бюджета на приёмы пищи
decimal breakfastLimit = dailyLimit * 0.25m; // ~25%
decimal lunchLimit     = dailyLimit * 0.40m; // ~40%
decimal dinnerLimit    = dailyLimit * 0.35m; // ~35%

// 3. База цен (примерно, усреднённые значения)
Dictionary<string, decimal> prices = new Dictionary<string, decimal>
{
    {"картофель (1 кг)", 20},
    {"курица (1 кг)", 120},
    {"яйца (10 шт)", 45},
    {"гречка (1 кг)", 60},
    {"молоко (1 л)", 35},
    {"хлеб (батон)", 25},
    {"рыба минтай (1 кг)", 110}
};

// 4. Формирование меню
// Для каждого дня выбираем блюда так, чтобы сумма <= dailyLimit
// Завтрак: каша/яйца/сырники
// Обед: суп + гарнир + мясо/рыба
// Ужин: овощи + лёгкое мясо/рыба или запеканка

// 5. Список покупок
// Суммируем ингредиенты по неделе
Dictionary<string, decimal> shoppingList = new Dictionary<string, decimal>();

void AddToShoppingList(string product, decimal qty)
{
    if (shoppingList.ContainsKey(product))
        shoppingList[product] += qty;
    else
        shoppingList[product] = qty;
}

// Пример: если в меню есть "борщ"
AddToShoppingList("картофель (1 кг)", 0.5m);
AddToShoppingList("морковь (1 кг)", 0.3m);
AddToShoppingList("свекла (1 кг)", 0.3m);
AddToShoppingList("капуста (1 кг)", 0.5m);
AddToShoppingList("свинина (1 кг)", 0.5m);

// 6. Проверка бюджета
decimal weeklySum = shoppingList.Sum(item => item.Value * prices[item.Key]);
if (weeklySum <= dailyLimit * 7)
{
    Console.WriteLine("Неделя укладывается в бюджет!");
}
else
{
    Console.WriteLine("Превышение бюджета, нужно заменить продукты.");
}
