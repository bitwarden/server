namespace Bit.Seeder.Data;

/// <summary>
/// First and last names organized by region for username generation.
/// Add new regions by creating arrays and including them in the All* properties.
/// </summary>
internal static class Names
{
    public static readonly string[] UsFirstNames =
    [
        // Male
        "James", "Robert", "John", "Michael", "David", "William", "Richard", "Joseph", "Thomas", "Christopher",
        "Charles", "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul", "Andrew", "Joshua",
        "Kenneth", "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Ryan",
        "Jacob", "Gary", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry", "Justin", "Scott", "Brandon",
        "Benjamin", "Samuel", "Raymond", "Gregory", "Frank", "Alexander", "Patrick", "Jack", "Dennis", "Jerry",
        // Female
        "Mary", "Patricia", "Jennifer", "Linda", "Barbara", "Elizabeth", "Susan", "Jessica", "Sarah", "Karen",
        "Lisa", "Nancy", "Betty", "Margaret", "Sandra", "Ashley", "Kimberly", "Emily", "Donna", "Michelle",
        "Dorothy", "Carol", "Amanda", "Melissa", "Deborah", "Stephanie", "Rebecca", "Sharon", "Laura", "Cynthia",
        "Kathleen", "Amy", "Angela", "Shirley", "Anna", "Brenda", "Pamela", "Emma", "Nicole", "Helen",
        "Samantha", "Katherine", "Christine", "Debra", "Rachel", "Carolyn", "Janet", "Catherine", "Maria", "Heather"
    ];

    public static readonly string[] UsLastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts",
        "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker", "Cruz", "Edwards", "Collins", "Reyes",
        "Stewart", "Morris", "Morales", "Murphy", "Cook", "Rogers", "Gutierrez", "Ortiz", "Morgan", "Cooper",
        "Peterson", "Bailey", "Reed", "Kelly", "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson",
        "Watson", "Brooks", "Chavez", "Wood", "James", "Bennett", "Gray", "Mendoza", "Ruiz", "Hughes",
        "Price", "Alvarez", "Castillo", "Sanders", "Patel", "Myers", "Long", "Ross", "Foster", "Jimenez"
    ];

    public static readonly string[] EuropeanFirstNames =
    [
        // British
        "Oliver", "George", "Harry", "Jack", "Charlie", "Thomas", "Oscar", "William", "James", "Henry",
        "Olivia", "Amelia", "Isla", "Ava", "Emily", "Sophie", "Grace", "Mia", "Poppy", "Ella",
        // German
        "Maximilian", "Alexander", "Paul", "Leon", "Lukas", "Felix", "Noah", "Elias", "Ben", "Finn",
        "Emma", "Hannah", "Mia", "Sofia", "Anna", "Emilia", "Lena", "Marie", "Lea", "Clara",
        // French
        "Gabriel", "Raphael", "Leo", "Louis", "Lucas", "Adam", "Hugo", "Jules", "Arthur", "Nathan",
        "Louise", "Alice", "Chloe", "Ines", "Lea", "Manon", "Rose", "Anna", "Lina", "Mila",
        // Spanish
        "Hugo", "Martin", "Lucas", "Daniel", "Pablo", "Alejandro", "Adrian", "Alvaro", "David", "Mario",
        "Lucia", "Sofia", "Maria", "Martina", "Paula", "Julia", "Daniela", "Valeria", "Alba", "Emma",
        // Italian
        "Leonardo", "Francesco", "Alessandro", "Lorenzo", "Mattia", "Andrea", "Gabriele", "Riccardo", "Tommaso", "Edoardo",
        "Sofia", "Giulia", "Aurora", "Alice", "Ginevra", "Emma", "Giorgia", "Greta", "Beatrice", "Anna"
    ];

    public static readonly string[] EuropeanLastNames =
    [
        // British
        "Smith", "Jones", "Williams", "Brown", "Taylor", "Davies", "Wilson", "Evans", "Thomas", "Johnson",
        "Roberts", "Walker", "Wright", "Robinson", "Thompson", "White", "Hughes", "Edwards", "Green", "Hall",
        // German
        "Mueller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann",
        "Schaefer", "Koch", "Bauer", "Richter", "Klein", "Wolf", "Schroeder", "Neumann", "Schwarz", "Zimmermann",
        // French
        "Martin", "Bernard", "Dubois", "Thomas", "Robert", "Richard", "Petit", "Durand", "Leroy", "Moreau",
        "Simon", "Laurent", "Lefebvre", "Michel", "Garcia", "David", "Bertrand", "Roux", "Vincent", "Fournier",
        // Spanish
        "Garcia", "Rodriguez", "Martinez", "Lopez", "Sanchez", "Gonzalez", "Perez", "Martin", "Gomez", "Ruiz",
        "Hernandez", "Jimenez", "Diaz", "Moreno", "Alvarez", "Munoz", "Romero", "Alonso", "Gutierrez", "Navarro",
        // Italian
        "Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo", "Ricci", "Marino", "Greco",
        "Bruno", "Gallo", "Conti", "DeLuca", "Costa", "Giordano", "Mancini", "Rizzo", "Lombardi", "Moretti"
    ];

    public static readonly string[] AllFirstNames = [.. UsFirstNames, .. EuropeanFirstNames];

    public static readonly string[] AllLastNames = [.. UsLastNames, .. EuropeanLastNames];
}
