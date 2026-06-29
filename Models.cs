namespace MenuApp;

record MealDay(string Date, string Breakfast, string Lunch, string Snack, string Dinner);
record PriceItem(string Name, decimal Price, string Unit, string Frequency = "еженедельно");
