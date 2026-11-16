namespace Bit.Seeder.Data.Static;

/// <summary>
/// The words that unique passwords are built from.
/// </summary>
internal static class PasswordWords
{
    /// <summary>
    /// 256 distinct lowercase words that a unique Strong or VeryStrong passphrase joins. No pooled passphrase can be
    /// spelled from them, so a passphrase never equals a pooled one. The first <see cref="CommonCount"/> are the
    /// <see cref="Common"/> words.
    /// </summary>
    internal static readonly string[] All =
    [
        "dragon", "monkey", "sunshine", "football", "shadow", "master", "flower", "summer",
        "winter", "spring", "autumn", "coffee", "tiger", "falcon", "rocket", "pirate",
        "garden", "silver", "golden", "hunter", "pepper", "cookie", "banana", "orange",
        "purple", "yellow", "thunder", "breeze", "harbor", "meadow", "castle", "forest",
        "island", "planet", "comet", "anchor", "violet", "maple", "cedar", "willow",
        "otter", "jaguar", "panda", "rabbit", "turtle", "dolphin", "eagle", "lion",
        "acorn", "alpine", "apple", "apricot", "arctic", "aspen", "atlas", "badger",
        "bamboo", "barley", "basil", "beacon", "beaver", "berry", "birch", "biscuit",
        "bison", "blossom", "bonfire", "bramble", "bridge", "bronze", "brook", "bubble",
        "butter", "cabin", "cactus", "camel", "candle", "canyon", "carbon", "carrot",
        "cavern", "cherry", "chestnut", "cinder", "citrus", "clover", "cobalt", "copper",
        "coral", "cotton", "coyote", "cricket", "crimson", "current", "daisy", "delta",
        "desert", "diamond", "dinner", "drift", "dune", "ermine", "fennel", "fiddle",
        "finch", "fjord", "flint", "fossil", "fountain", "galaxy", "garlic", "geyser",
        "ginger", "glacier", "goblet", "granite", "grove", "gull", "hammock", "hazel",
        "heron", "hickory", "hollow", "honey", "horizon", "iceberg", "indigo", "ivory",
        "jasmine", "jungle", "juniper", "kayak", "kettle", "kitten", "lagoon", "lantern",
        "lavender", "lemon", "lilac", "lizard", "lobster", "lotus", "magnet", "mango",
        "marble", "marlin", "meteor", "mirror", "mosaic", "mountain", "mustard", "nectar",
        "nickel", "nutmeg", "oasis", "olive", "onyx", "orchid", "osprey", "oyster",
        "paddle", "pebble", "pelican", "penguin", "pickle", "pigeon", "pillow", "pine",
        "plum", "pocket", "poppy", "prairie", "pumpkin", "puzzle", "quail", "quartz",
        "raven", "reef", "ribbon", "ripple", "saddle", "saffron", "salmon", "sapphire",
        "scarlet", "sequoia", "shell", "sierra", "sparrow", "spruce", "squirrel", "starling",
        "summit", "sundial", "swallow", "tangerine", "teapot", "thistle", "timber", "topaz",
        "trout", "tulip", "tundra", "turnip", "umber", "vinegar", "walnut", "walrus",
        "waffle", "whisper", "wombat", "yarrow", "zephyr", "zebra", "zenith", "almond",
        "bagel", "beetle", "bobcat", "button", "cashew", "cheddar", "cloak", "compass",
        "cranberry", "crayon", "dahlia", "drizzle", "falls", "feather", "gadget", "gravel",
        "gopher", "harvest", "igloo", "jigsaw", "kernel", "ladder", "lentil", "meringue",
        "noodle", "oatmeal", "parsley", "peach", "quiver", "rhubarb", "sailor", "sprout",
        "teacup", "tomato", "trumpet", "unicorn", "vanilla", "weasel", "whistle", "yogurt"
    ];

    /// <summary>
    /// How many of the first words of <see cref="All"/> are common ones.
    /// </summary>
    private const int CommonCount = 48;

    /// <summary>
    /// The first <see cref="CommonCount"/> words of <see cref="All"/>, which a unique Fair password starts with,
    /// capitalized and followed by digits. zxcvbn scores the rarer words of <see cref="All"/> higher, which moves
    /// Fair away from the Fair pool's scores.
    /// </summary>
    internal static readonly string[] Common = All[..CommonCount];
}
