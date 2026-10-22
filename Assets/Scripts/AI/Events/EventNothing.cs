using System.Collections.Generic;

/// <summary>Response pools for "you got nothing": NPC events (spoken by the NPC, 8 contextual + 16 generic lines each) and standalone
/// events (backpack, wallet, papers, pigeons: plain narration, 20+ lines each). Never repeats the same line twice in a row per event.</summary>
public static class EventNothing
{
    static readonly Dictionary<string, string[]> pools = new Dictionary<string, string[]>();
    static readonly Dictionary<string, string> last = new Dictionary<string, string>();

    // ---------- standalone (no NPC): narration ----------
    static readonly string[] Backpack = {
        "The backpack was empty.", "Just damp leaves and a broken zipper.", "There was nothing useful inside.", "Only crumbs and an old receipt.",
        "Someone had already picked it clean.", "A single sock. That's it.", "The pockets were all empty.", "Nothing but mouldy sandwich wrappers.",
        "It was full of rocks. Just rocks.", "An empty water bottle and nothing else.", "The lining was torn and the bag was bare.", "Not a single thing worth keeping.",
        "Just a soggy map that fell apart.", "The bag was lighter than it looked. Empty.", "Only a dead flashlight inside.", "Insects had moved in. Nothing else.",
        "A tangle of cables, none of them useful.", "Whoever owned it took everything of value.", "Nothing here but dust and pine needles.", "The bag had been chewed through by something. Empty.",
        "A few torn pages, nothing useful.", "You searched every pocket. Nothing." };
    static readonly string[] Wallet = {
        "The wallet was empty.", "Only a few expired coupons inside.", "There was nothing useful in the wallet.", "Just a library card from years ago.",
        "No cash, no cards, just lint.", "A faded photo and nothing else.", "Only loyalty stamps, all but one missing.", "The ID was too scratched to read, and nothing else was inside.",
        "Someone had already taken the cash.", "A single button. That's all.", "Just receipts, all of them blank.", "The cards inside were all cancelled.",
        "A bus ticket from last month, nothing more.", "It had been cleaned out long ago.", "Nothing but a folded shopping list.", "Only pennies, and not enough to bother with.",
        "The coin pocket was empty too.", "Just a gym card that expired years ago.", "A fake coupon for a store that closed.", "Nothing inside. Not even a name.",
        "It was a toy wallet. Funny.", "You checked every slot. Nothing." };
    static readonly string[] Papers = {
        "The papers didn't make sense.", "Just blank pages and coffee stains.", "Half of them were soaked and unreadable.", "A pile of meeting notes, nothing useful.",
        "All the pages were in a language you didn't know.", "Only old invoices, nothing worth keeping.", "The ink had run. Nothing readable.", "Just a printed spreadsheet, no use at all.",
        "Page after page of scribbles.", "They were someone's grocery lists.", "Just junk mail and flyers.", "The pages were out of order and made no sense.",
        "Pure gibberish, as if typed by a cat.", "Only drafts of a boring memo.", "Nothing but old schedules.", "The pages were torn into unreadable scraps.",
        "A stack of expired coupons.", "Just form letters, nothing useful.", "Every sheet was blank on the back too.", "Numbers and charts that meant nothing to you.",
        "Someone's doodles, nothing more.", "You gathered them all. Nothing useful in them." };
    static readonly string[] Pigeons = {
        "The pigeons cooed and wandered off. Nothing for you.", "They ate the crumbs and left you nothing.", "Not even a shiny button from the flock.", "The pigeons only wanted bread. No gifts.",
        "They bobbed their heads and flew away.", "The flock had nothing to give you.", "Just feathers. Nothing useful.", "A grateful coo, and that's all.",
        "The birds finished and scattered without a trace.", "Not a single pigeon left a present.", "They pecked the ground clean and that was that.", "You made some friends. No loot.",
        "One pigeon stared at you, then left.", "Nothing but a few loose feathers.", "The pigeons were more interested in the bread.", "They walked away like nothing happened.",
        "A feather fell at your feet. That's it.", "The flock had already taken everything shiny.", "Cooing, flapping, and nothing else.", "The birds didn't leave anything behind." };

    // ---------- NPC events: generic ----------
    static readonly string[] Generic = {
        "Sorry, I've got nothing to give you.", "I wish I had something for you. I really don't.", "My pockets are empty, sorry.", "Thanks anyway. I've got nothing on me.",
        "I'd reward you if I could. I can't.", "That's all I have to offer: my thanks.", "I'm out of everything, I'm afraid.", "Sorry, I came out with nothing at all.",
        "Nothing to hand over today, I'm sorry.", "You've been kind. I just have nothing to give.", "If I had anything, it'd be yours.", "I left everything at home, sorry.",
        "Wish I could pay you back. Not today.", "I'm all out, friend. Thank you though.", "Honestly, I've got nothing left.", "Bless you. That's all I can give." };

    static EventNothing()
    {
        pools["lost_backpack"] = Backpack; pools["lost_wallet"] = Wallet; pools["flying_documents"] = Papers; pools["pigeon_king"] = Pigeons;
        K("grandma_groceries", "Oh dear, even my purse is in the bags you carried.", "The groceries were all I had, dear.", "I spent my last coin on these bags.", "Nothing left but the shopping, I'm afraid.", "My pension doesn't stretch to tips, dear.", "Those bags were heavy, and I've nothing to give you.", "The apples are counted. Sorry, dear.", "I'd give you a biscuit, but they're gone.");
        K("lost_dog", "I've no treats left. Biscuit used his up.", "My dog is safe, and I have nothing else to offer.", "I only brought the lead, sorry.", "Thanks for finding him! My pockets are empty.", "He ate everything I had on me.", "I'd give you a bone, but it's his.", "All I've got is a squeaky toy. Not for you.", "I rushed out without my wallet, sorry.");
        K("garage_sale_couch", "That couch was my best item, and I've nothing else.", "Sorry, sold everything else this morning.", "The cash box is empty, I'm afraid.", "I only have bits of junk left. Not worth giving.", "Everything's marked 'sold'. Nothing to hand you.", "The couch moved, my wallet didn't.", "Garage sale's over, and so is my stock.", "I'd offer a lamp, but it's cracked.");
        K("thirsty_plants", "The plants are happy. I've nothing for you.", "I gave the last of my seeds to the pots.", "Only watering cans here, sorry.", "My garden is bare of gifts today.", "All I have is soil. Not much of a reward.", "Everything's blooming, but nothing to pick for you.", "I've no cuttings left, sorry.", "The petals are all I have.");
        K("lawnmower", "I've used up the last of my fuel and my cash.", "Nothing but grass clippings to offer.", "The shed's empty, sorry about that.", "It runs again! I just have nothing to give you.", "I fixed my budget on this mower, sorry.", "I'd give you a spanner, but I need it.", "All I've got is oil on my hands.", "The toolbox is bare today.");
        K("stalled_car", "The engine's alive, but my wallet isn't.", "I'm out of cash, and I'm low on fuel too.", "Sorry, I've only got an empty glovebox.", "Thanks for the push. I've nothing to hand over.", "I'd pay you if I weren't broke.", "All I have is a flat tyre and my gratitude.", "The tow money took everything.", "Only a road map in here. Sorry.");
        K("lemonade_stand", "We're sold out of everything, even the lemons.", "The jar of coins is empty, sorry.", "No lemonade left. Not a drop.", "Sorry, I squeezed my last lemon.", "The cup's empty. So is my pocket.", "Come back tomorrow, I'll have lemonade.", "I sold the last glass, sorry.", "I only have sugar. That's no reward.");
        K("sprinkler_valve", "The sprinklers work again. I've got nothing else.", "I'm drenched and flat broke, sorry.", "I only have a wet rag to offer.", "The toolbox floated away, sorry.", "Everything I owned got soaked.", "All I have is a pipe wrench I need.", "The grass says thank you. I've nothing else.", "My wallet's waterlogged. Nothing in it.");
        K("hiker_ankle", "I've nothing left, my pack's empty, sorry.", "I ate my last snack on the trail.", "I'm out of bandages and gifts, sorry.", "The first aid kit's bare now.", "All I have is sore feet and thanks.", "My water bottle's empty too.", "I left my gear back at camp, sorry.", "Thanks. I wish I had something to give.");
        K("injured_deer", "The deer is safe. I've nothing else to give.", "Wrapped the last bandage, sorry.", "Only herbs I need for the fawn.", "The forest gives gifts, but I've none today.", "I'm only a ranger with an empty satchel.", "Nothing to give but a nod from the deer.", "She's up and running. I have no reward.", "I used all my supplies on her, sorry.");
        K("druid_dance", "The spirits danced, and I have nothing for you.", "The moon gives gifts, but not tonight.", "I only have leaves and mud, sorry.", "The grove is quiet. No blessings left.", "My charms are all spent.", "The ritual drained my last token.", "The forest took everything I had.", "Only my thanks, dear traveller.");
        K("witch_cauldron", "The brew is done. I've nothing left for you.", "That batch used my last ingredient.", "The cauldron's empty, so is my shelf.", "Not even a frog leg to spare, sorry.", "All potions are sold, I'm afraid.", "My spellbook is all I own, and it's not for sale.", "Nothing but smoke, dearie.", "I've no charms left today.");
        K("beekeeper", "No honey left, the bees took it all.", "The jars are empty, sorry.", "The hives gave nothing this season.", "I only have wax scraps.", "All my honey's already promised, sorry.", "The bees are busy and so am I. Nothing to give.", "Only a sting to remember me by.", "I'd give you a comb, but it's empty.");
        K("mushroom_sorting", "We've sorted everything, and I've nothing left.", "Only the poisonous ones are left. Not for you.", "The basket's empty, sorry.", "All the good ones are already sold.", "I've nothing but spores to give.", "The market took the last of my harvest.", "Not a single cap left, sorry.", "Thanks for the sorting. I'm out of stock.");
        K("hermit_riddle", "Wisdom was all I had to give. I've nothing else.", "A hermit owns nothing. Sorry.", "The answer was the prize. There's no more.", "I live in a cave. What do you expect?", "My only treasure is silence.", "Only riddles here. No gifts.", "I own a cloak, and I'm wearing it.", "You've earned my respect. That's all I have.");
        K("fallen_log", "The trail is clear, and my pack is empty.", "I only have splinters to give.", "Thanks for the haul. I've nothing on me.", "Used my last rope on this log, sorry.", "I'm out of supplies, sorry.", "All I have is sap on my hands.", "I'd give you a branch, but there are plenty.", "My pockets are as bare as that trunk.");
        K("boulder", "The path is open, and my pockets are empty.", "I only have pebbles to offer.", "Used my last rope on that rock, sorry.", "Nothing to give but dust, friend.", "That boulder ate my energy and my cash.", "I've nothing left, only gratitude.", "All I own is a bruised back.", "The rock was my last worry, not my last coin. Sorry.");
        K("delivery_boxes", "Everything's accounted for, nothing spare, sorry.", "The packages were all I carried.", "No tips today. Company rules, sorry.", "I'd give you a package, but it's not mine.", "I'm out of stickers and snacks, sorry.", "All the boxes are signed for. Nothing extra.", "My van is empty now.", "Thanks. I'm only a courier with a clipboard.");
        K("runaway_cart", "The cart rolls again, but my bag is empty.", "Only receipts in the basket, sorry.", "I've nothing in this cart worth giving.", "The shopping's all I have, sorry.", "The wheel's fixed, my wallet isn't.", "Sorry, only empty bags left.", "I bought the last thing on my list.", "All I have is a squeaky wheel.");
        K("vending_machine", "The machine's empty now, and so am I.", "It ate my last coin and gave nothing.", "That was the last snack, sorry.", "Not even a crumb left to give.", "I'd share, but I ate it already.", "My change is all in the machine.", "Machine says 'sold out'. So do I.", "I'm hungry and broke, sorry.");
        K("food_truck", "Lunch rush ate everything. Nothing left.", "The onions were my last ingredient.", "We're sold out, sorry.", "The tip jar is empty today.", "All the plates are gone, sorry.", "The grill's cold, and so is my till.", "I can't spare a bite, sorry.", "Only chopped onions left, and they're for me.");
        K("street_magician", "A magician never gives away his last trick.", "The hat is empty, I'm afraid.", "My tricks need props, and I've none left.", "Nothing up my sleeve. Truly, nothing.", "The rabbit retired. So did my gifts.", "That was my best trick, and my last.", "Only cards left, and they're marked.", "The show's over. So is my stock.");
        K("tourist_map", "I only have a map, and I need it.", "I spent all my cash on souvenirs, sorry.", "The tour bus took everything.", "Thanks for the directions. I'm broke.", "I've only foreign coins, sorry.", "My luggage is lost, so is my gift.", "All I have is a postcard. It's mine.", "I'd give you a keychain, but I gave it away.");
        K("alien_tourist", "Earth trade is confusing. I've nothing to give.", "My ship has gifts, but it's far away.", "I have only star dust, and you can't carry it.", "Your answers delighted me. No gift, sorry.", "My currency is worthless here.", "I forgot the gift basket on the ship.", "Thank you, human. I come empty-handed.", "On my planet, thanks is the gift.");
    }

    static void K(string id, params string[] ctx)
    {
        string[] all = new string[ctx.Length + Generic.Length];
        ctx.CopyTo(all, 0); Generic.CopyTo(all, ctx.Length);
        pools[id] = all;
    }

    public static string Pick(string id, System.Random rng)
    {
        string[] p;
        if (!pools.TryGetValue(id, out p)) p = Generic;
        string s = p[rng.Next(p.Length)];
        string prev;
        if (p.Length > 1 && last.TryGetValue(id, out prev) && prev == s) s = p[(System.Array.IndexOf(p, s) + 1) % p.Length];
        last[id] = s;
        return s;
    }

    public static bool IsStandalone(string id) { return id == "lost_backpack" || id == "lost_wallet" || id == "flying_documents" || id == "pigeon_king"; }
}
