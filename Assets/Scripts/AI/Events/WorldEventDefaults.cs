using System.Collections.Generic;
using UnityEngine;

public static class WorldEventDefaults
{
    static List<WorldEventConfig> cache;

    public static List<WorldEventConfig> All
    {
        get
        {
            if (cache == null) cache = Build();
            return cache;
        }
    }

    static Color Hex(string hex)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.gray;
    }

    static WorldEventConfig D(EventZone zone, string id, string label, string prompt, string ask, string ok, string no,
        EventHandlerKind handler, float amount, float limit, char kind, string shirt, PrimitiveKind pk, string propHex,
        float sx, float sy, float sz, PlayerEventAnim during)
    {
        WorldEventConfig c = ScriptableObject.CreateInstance<WorldEventConfig>();
        c.name = id;
        c.id = id;
        c.zone = zone;
        c.npcLabel = label;
        c.prompt = prompt;
        c.ask = ask;
        c.success = ok;
        c.fail = no;
        c.handler = handler;
        c.amount = amount;
        c.limit = limit;
        c.reward = kind == 's' ? 0.35f : (kind == 'h' ? 0.6f : 0.85f);
        c.shirt = Hex(shirt);
        c.propKind = pk;
        c.propColor = Hex(propHex);
        c.propSize = new Vector3(sx, sy, sz);
        c.playerDuring = during;
        Fill(c);
        return c;
    }

    public static WorldEventConfig Find(string id)
    {
        foreach (WorldEventConfig c in All) if (c.id == id) return c;
        return null;
    }

    static ChoiceQuestion Q(string q, int correct, params string[] options)
    {
        return new ChoiceQuestion { question = q, correct = correct, options = options };
    }

    static SortItem M(string name, string hex, int category)
    {
        return new SortItem { name = name, color = Hex(hex), category = category };
    }

    static void Fill(WorldEventConfig c)
    {
        switch (c.id)
        {
            case "hermit_riddle":
                c.choice.asked = 1;
                c.choice.questions.Add(Q("I have cities but no houses, forests but no trees, and water but no fish. What am I?", 0, "A map", "A dream", "A painting"));
                c.choice.questions.Add(Q("The more of me you take, the more you leave behind. What am I?", 1, "Coins", "Footsteps", "Cookies"));
                c.choice.questions.Add(Q("I speak without a mouth and hear without ears. What am I?", 2, "A ghost", "A bell", "An echo"));
                c.choice.questions.Add(Q("What has keys but opens no locks, and has space but no room?", 0, "A keyboard", "A piano bench", "A jail"));
                c.choice.questions.Add(Q("I have hands but cannot clap. What am I?", 1, "A statue", "A clock", "A glove"));
                c.choice.questions.Add(Q("What gets wetter the more it dries?", 2, "A sponge", "A puddle", "A towel"));
                c.choice.questions.Add(Q("I am tall when I am young and short when I am old. What am I?", 0, "A candle", "A tree", "A shadow"));
                c.choice.questions.Add(Q("What has a head and a tail but no body?", 1, "A snake", "A coin", "A comet"));
                c.choice.questions.Add(Q("The more of me there is, the less you can see. What am I?", 2, "Fog on a lamp", "Smoke", "Darkness"));
                c.choice.questions.Add(Q("What can travel around the world while staying in one corner?", 0, "A stamp", "A compass", "A kite"));
                c.choice.questions.Add(Q("I have a spine but no bones, and I hold many stories. What am I?", 1, "A cactus", "A book", "A fish"));
                c.choice.questions.Add(Q("What runs but never walks, and has a mouth but never talks?", 2, "A dog", "A faucet", "A river"));
                c.choice.questions.Add(Q("What belongs to you, but other people use it more than you do?", 0, "Your name", "Your phone", "Your coat"));
                c.choice.questions.Add(Q("I fly without wings and cry without eyes. What am I?", 1, "A kite", "A cloud", "A balloon"));
                c.choice.questions.Add(Q("What has teeth but cannot bite?", 2, "A saw blade", "A zipper", "A comb"));
                c.choice.questions.Add(Q("What goes up when the rain comes down?", 0, "An umbrella", "A puddle", "A balloon"));
                c.choice.questions.Add(Q("What has one eye but cannot see?", 1, "A cyclops", "A needle", "A storm"));
                c.choice.questions.Add(Q("The more you feed me, the more I grow, but give me water and I die. What am I?", 2, "A plant", "A pet", "A fire"));
                c.choice.questions.Add(Q("What can you catch but never throw?", 0, "A cold", "A ball", "A fish net"));
                c.choice.questions.Add(Q("What has many rings but no fingers?", 1, "A tree trunk", "A telephone", "A tub"));
                break;
            case "tourist_map":
                c.choice.asked = 1;
                c.choice.questions.Add(Q("The museum is north of the station and the park is east of it. I am standing in the park. Which way is the museum?", 0, "North-west", "South-east", "North-east", "South-west"));
                c.choice.questions.Add(Q("The cafe is south of the library and the bank is west of the library. I am standing at the bank. Which way is the cafe?", 1, "North-east", "South-east", "South-west", "North-west"));
                c.choice.questions.Add(Q("The hotel is east of the pier and the market is north of the pier. I am at the market. Which way is the hotel?", 2, "North-west", "North-east", "South-east", "South-west"));
                c.choice.questions.Add(Q("The school is west of the fountain and the cinema is south of the fountain. I am at the cinema. Which way is the school?", 3, "South-east", "South-west", "North-east", "North-west"));
                c.choice.questions.Add(Q("The bakery is north of the bridge and the hospital is west of the bridge. I am at the hospital. Which way is the bakery?", 0, "North-east", "North-west", "South-east", "South-west"));
                c.choice.questions.Add(Q("The tower is south of the post office and the zoo is east of the post office. I am at the zoo. Which way is the tower?", 1, "South-east", "South-west", "North-west", "North-east"));
                c.choice.questions.Add(Q("I walked two blocks north, then two blocks east. Which way is my starting point from here?", 2, "North-east", "North-west", "South-west", "South-east"));
                c.choice.questions.Add(Q("I walked three blocks south, then three blocks west. Which way do I go to get back to the start?", 0, "North-east", "South-east", "North-west", "South-west"));
                c.choice.questions.Add(Q("I walked one block east, one block north, then one block west. Where am I compared with where I started?", 1, "Directly east of the start", "Directly north of the start", "At the start", "South-west of the start"));
                c.choice.questions.Add(Q("I am facing north and I turn right. Which way am I facing now?", 2, "West", "South", "East"));
                c.choice.questions.Add(Q("I am facing east and I turn left. Which way am I facing now?", 0, "North", "South", "West"));
                c.choice.questions.Add(Q("I am facing south, turn all the way around, then turn left. Which way am I facing?", 1, "East", "West", "South", "North"));
                c.choice.questions.Add(Q("The sun rises in the east. If I walk toward the morning sun, which way am I heading?", 2, "West", "North", "East"));
                c.choice.questions.Add(Q("On most maps, which direction points to the top of the page?", 0, "North", "East", "South"));
                c.choice.questions.Add(Q("The museum is three blocks north of the station. I leave the station and walk one block north. How many blocks are left?", 1, "3 blocks", "2 blocks", "4 blocks", "1 block"));
                c.choice.questions.Add(Q("The pharmacy is directly west of the diner, and the diner is directly west of the church. I am at the church. Which way is the pharmacy?", 3, "East", "North", "South", "West"));
                break;
            case "alien_tourist":
                c.choice.asked = 3;
                c.choice.questions.Add(Q("What do humans put on their feet before their shoes?", 1, "Gloves", "Socks", "Hats"));
                c.choice.questions.Add(Q("Why do humans stop when a signal turns red?", 2, "They enjoy the colour", "The road is closed forever", "It tells them to wait for safety"));
                c.choice.questions.Add(Q("What is a key used for?", 0, "Opening a lock", "Cooking food", "Calling friends"));
                c.choice.questions.Add(Q("What is an umbrella for?", 1, "Digging holes", "Staying dry in the rain", "Carrying water"));
                c.choice.questions.Add(Q("What is a pillow for?", 2, "Sitting on a roof", "Cleaning windows", "Resting the head"));
                c.choice.questions.Add(Q("Why do humans wear coats in winter?", 0, "To stay warm", "To hide from birds", "To float"));
                c.choice.questions.Add(Q("What do humans do with a toothbrush?", 1, "Paint fences", "Clean their teeth", "Comb their hair"));
                c.choice.questions.Add(Q("What is a clock for?", 2, "Cooling soup", "Catching bugs", "Telling the time"));
                c.choice.questions.Add(Q("Why do humans light candles on a birthday cake?", 0, "To celebrate and make a wish", "To cook the cake", "To scare mice"));
                c.choice.questions.Add(Q("What do humans do at a bus stop?", 1, "Plant flowers", "Wait for a bus", "Fish for dinner"));
                c.choice.questions.Add(Q("What is a map used for?", 2, "Wrapping presents", "Wiping tables", "Finding the way"));
                c.choice.questions.Add(Q("Why do humans say 'thank you'?", 0, "To show they are grateful", "To end the conversation forever", "To ask for more food"));
                c.choice.questions.Add(Q("What is a spoon used for?", 1, "Hammering nails", "Eating soup", "Writing letters"));
                c.choice.questions.Add(Q("What do humans do when they feel sleepy at night?", 2, "Run a marathon", "Climb a tree", "Go to bed"));
                c.choice.questions.Add(Q("What is a phone mostly used for?", 0, "Talking to people far away", "Boiling water", "Digging soil"));
                c.choice.questions.Add(Q("Why do humans wear sunglasses on sunny days?", 1, "To hear better", "To protect their eyes from glare", "To keep their feet dry"));
                c.choice.questions.Add(Q("What do humans do with a book?", 2, "Water it", "Wear it", "Read it"));
                c.choice.questions.Add(Q("What is a bicycle?", 0, "A vehicle you pedal", "A kind of soup", "A tiny house"));
                c.choice.questions.Add(Q("Why do humans shake hands when they meet?", 1, "To measure each other", "To greet each other politely", "To dry their hands"));
                c.choice.questions.Add(Q("What happens to ice cream if you leave it in the sun?", 2, "It grows bigger", "It turns into a stone", "It melts"));
                c.choice.questions.Add(Q("What is a bed for?", 0, "Sleeping", "Swimming", "Frying eggs"));
                c.choice.questions.Add(Q("What do humans use a fork for?", 1, "Brushing teeth", "Picking up food", "Tying shoes"));
                c.choice.questions.Add(Q("Why do humans queue in a line at a shop?", 2, "They are lost", "It is a type of dance", "To take turns fairly"));
                c.choice.questions.Add(Q("What do humans do with soap?", 0, "Wash themselves and things", "Eat it for lunch", "Plant it in the garden"));
                c.choice.questions.Add(Q("What is a ladder used for?", 1, "Sleeping upside down", "Reaching high places", "Catching rain"));
                c.choice.questions.Add(Q("Why do humans carry a wallet?", 2, "To feed the birds", "To keep their hat on", "To keep money and cards"));
                c.choice.questions.Add(Q("What do humans do with a mirror?", 0, "Look at their reflection", "Bake bread", "Whistle into it"));
                c.choice.questions.Add(Q("What is a traffic light's green for?", 1, "Stop right now", "It is safe to go", "Take a nap"));
                break;
            case "mushroom_sorting":
                c.sorting.categories = new[] { "Safe", "Poisonous" };
                c.sorting.allowedMistakes = 1;
                c.sorting.items.Add(M("Porcini", "#8B5A2B", 0));
                c.sorting.items.Add(M("Chanterelle", "#E8A317", 0));
                c.sorting.items.Add(M("Button mushroom", "#EDE6D6", 0));
                c.sorting.items.Add(M("Parasol", "#B89A78", 0));
                c.sorting.items.Add(M("Fly agaric", "#D12B2B", 1));
                c.sorting.items.Add(M("Death cap", "#9BAA5A", 1));
                c.sorting.items.Add(M("Panther cap", "#6B4A32", 1));
                c.sorting.items.Add(M("Destroying angel", "#F4F4F0", 1));
                break;
        }
    }

    public static List<WorldEventConfig> Build()
    {
        EventZone S = EventZone.Suburb, F = EventZone.Forest, C = EventZone.City;
        EventHandlerKind H = EventHandlerKind.Hold, M = EventHandlerKind.Mash, Q = EventHandlerKind.Sequence;
        EventHandlerKind T = EventHandlerKind.Timing, Ch = EventHandlerKind.Choice, So = EventHandlerKind.Sorting, O = EventHandlerKind.Object;
        PrimitiveKind Cu = PrimitiveKind.Cube, Sp = PrimitiveKind.Sphere, Cy = PrimitiveKind.Cylinder;
        PlayerEventAnim Li = PlayerEventAnim.Lift, Ca = PlayerEventAnim.Calm, Pu = PlayerEventAnim.Push, Ti = PlayerEventAnim.Tinker,
            Su = PlayerEventAnim.Support, Po = PlayerEventAnim.Point, In = PlayerEventAnim.Inspect;

        List<WorldEventConfig> l = new List<WorldEventConfig>();
        l.Add(D(S, "grandma_groceries", "GRANDMA", "HELP THE GRANDMA", "Dear, could you carry my groceries inside?", "Bless you, sweetheart. Take something for your trouble.", "Oh, never mind, I will manage.", H, 3f, 0f, 's', "#C98FB0", Cu, "#C8A060", 0.5f, 0.4f, 0.4f, Li));
        l.Add(D(S, "lost_dog", "WORRIED OWNER", "CALM THE DOG", "My dog is scared. Can you calm him down?", "He trusts you! Here, a little thank you.", "He ran off again...", H, 4f, 0f, 's', "#5F7FBF", Sp, "#B07F4A", 0.5f, 0.4f, 0.7f, PlayerEventAnim.PatDog));
        l.Add(D(S, "garage_sale_couch", "GARAGE SALE LADY", "LIFT THE COUCH", "Hold this couch while I clear the door!", "Sold! And this is for the muscle.", "Ugh, my back. Forget it.", H, 8f, 0f, 'h', "#B07F4A", Cu, "#7F4F3F", 1.8f, 0.8f, 0.9f, Li));
        l.Add(D(S, "thirsty_plants", "NEIGHBOR", "WATER THE PLANTS", "My plants are wilting. Could you water them with me?", "They look so happy! Here, take this.", "They will have to wait for rain.", H, 5f, 0f, 's', "#6FA060", Cy, "#A0603F", 0.4f, 0.5f, 0.4f, PlayerEventAnim.Pour));
        l.Add(D(S, "lawnmower", "NEIGHBOR", "HELP THE NEIGHBOR", "This mower will not start. Pull it for me?", "Finally! You are a lifesaver.", "Ah, forget it. It is broken.", M, 14f, 6f, 's', "#4F8F4F", Cu, "#C84B31", 0.6f, 0.5f, 0.9f, PlayerEventAnim.Pull));
        l.Add(D(S, "stalled_car", "STRANDED DRIVER", "PUSH THE CAR", "My car died. Help me push it off the road!", "Thanks, I owe you one. Take this.", "It is too heavy. I will call a tow.", M, 26f, 10f, 'h', "#8F3030", Cu, "#3F6FB5", 1.8f, 1.4f, 4.2f, Pu));
        l.Add(D(S, "lemonade_stand", "LEMONADE KID", "SQUEEZE LEMONS", "Help me squeeze lemons before the rush!", "Best lemonade ever! Take this as a tip.", "No lemonade today...", M, 12f, 7f, 's', "#E8D040", Cu, "#E8E040", 0.8f, 0.8f, 0.8f, Ti));
        l.Add(D(S, "sprinkler_valve", "SOGGY DAD", "FIX THE SPRINKLERS", "The sprinklers are flooding the lawn. Turn the valves!", "Dry at last! Here, take this.", "Now the whole yard is a lake.", Q, 4f, 8f, 's', "#3F7FBF", Cy, "#8F9FAF", 0.3f, 0.4f, 0.3f, PlayerEventAnim.Turn));

        l.Add(D(F, "hiker_ankle", "LOST HIKER", "HELP THE HIKER", "I twisted my ankle. Can you support me?", "Thank you. I found this on the trail, take it.", "I will crawl back, I guess.", H, 5f, 0f, 's', "#D06030", Cu, "#3F6F3F", 0.5f, 0.6f, 0.3f, Su));
        l.Add(D(F, "injured_deer", "RANGER", "HELP THE DEER", "This deer is hurt. Help me mend it.", "He will be fine. Here, take this.", "He slipped away from us.", H, 5f, 0f, 's', "#3F7F4F", Sp, "#A0703F", 1f, 0.6f, 1.4f, Su));
        l.Add(D(F, "druid_dance", "DRUID", "JOIN THE RITUAL", "Dance with us beneath the old oak. Mirror my steps.", "The oak is pleased. Take its blessing.", "The oak is offended. Go away.", Q, 8f, 12f, 'w', "#2F6F3F", Cu, "#C0A040", 0.4f, 0.8f, 0.4f, PlayerEventAnim.Dance));
        l.Add(D(F, "witch_cauldron", "FOREST WITCH", "STIR THE CAULDRON", "Stir my cauldron! Press E when the pointer is in the green!", "Bubbling perfectly. Here, a little something.", "It curdled. Out of my sight!", T, 3f, 0f, 'w', "#4F2F6F", Sp, "#303030", 0.8f, 0.7f, 0.8f, PlayerEventAnim.Stir));
        l.Add(D(F, "beekeeper", "BEEKEEPER", "HELP THE BEEKEEPER", "Keep the smoker steady while I check the hive.", "Sweet work. Here, take this honey-stained gift.", "The bees did not like that.", T, 3f, 0f, 's', "#E0E0C0", Cu, "#E0B040", 0.6f, 0.8f, 0.6f, Ti));
        l.Add(D(F, "mushroom_sorting", "FORAGER", "SORT MUSHROOMS", "Help me sort the safe mushrooms from the bad.", "Sorted! Here, a gift from the forest.", "I will not risk eating those.", So, 5f, 0f, 's', "#8F6F4F", Sp, "#D04F4F", 0.4f, 0.3f, 0.4f, Po));
        l.Add(D(F, "hermit_riddle", "HERMIT", "ANSWER THE HERMIT", "Solve my riddle and I will reward you.", "Wise one. Take this from my cave.", "Bah. Leave me with my rock.", Ch, 1f, 0f, 'h', "#6F6F5F", Sp, "#7F7F7F", 1.2f, 0.9f, 1.2f, Po));
        l.Add(D(F, "fallen_log", "LUMBERJACK", "DRAG THE LOG", "This log blocks the trail. Grab an end and help me drag it to the marker!", "Timber! You earned this.", "Too heavy. I will fetch the saw.", M, 28f, 12f, 'h', "#BF3030", Cy, "#6F4F2F", 3f, 0.7f, 0.7f, Pu));
        l.Add(D(F, "boulder", "WANDERER", "MOVE THE BOULDER", "This boulder blocks my way. Help me push it!", "Many thanks, stranger. Take this.", "Bah. Leave me with my rock.", M, 24f, 10f, 'h', "#6F5F4F", Sp, "#7F7F7F", 1.2f, 1f, 1.2f, Pu));
        l.Add(D(F, "lost_backpack", "WORRIED CAMPER", "CHECK THE BACKPACK", "I lost my backpack. Can you check it for me?", "That is it! Here, take something for the trouble.", "Never mind, it is gone.", O, 2f, 0f, 's', "#3F5F8F", Cu, "#C0702F", 0.4f, 0.5f, 0.3f, In));

        l.Add(D(C, "delivery_boxes", "COURIER", "CARRY THE BOXES", "My arms are full. Hold these boxes for me!", "Delivered! This one fell off the truck, take it.", "I will make two trips then.", H, 5f, 0f, 's', "#8F6F3F", Cu, "#B58B50", 0.6f, 0.6f, 0.6f, Li));
        l.Add(D(C, "pigeon_king", "PIGEONS", "FEED THE PIGEONS", "These pigeons look hungry. Toss them some breadcrumbs.", "They gobbled up every crumb! Here, a little thank you.", "The pigeons scattered before they could eat.", H, 6f, 0f, 'w', "#7F7F8F", Sp, "#8F8F9F", 0.4f, 0.3f, 0.4f, Ca));
        l.Add(D(C, "runaway_cart", "SHOPPER", "PUSH THE CART", "My cart is stuck. Can you help me push it?", "Phew! Thank you so much. Take this.", "It will not budge. I will call the store.", M, 14f, 5f, 's', "#BF5F8F", Cu, "#A0A0B0", 0.6f, 1f, 1f, Pu));
        l.Add(D(C, "vending_machine", "HUNGRY OFFICE WORKER", "BANG THE MACHINE", "My snack is stuck. Bang the machine!", "It fell! Take this for the help.", "It ate my money and my dignity.", M, 12f, 6f, 's', "#5F5F7F", Cu, "#B03030", 0.9f, 1.8f, 0.8f, PlayerEventAnim.Bang));
        l.Add(D(C, "food_truck", "FOOD TRUCK CHEF", "CHOP THE ONIONS", "Lunch rush! Chop onions, chop chop!", "Fast hands! Here, extra for you.", "The line left. I am ruined.", M, 22f, 7f, 'h', "#E0E0E0", Cu, "#D0A040", 2.2f, 1.6f, 3f, Ti));
        l.Add(D(C, "street_magician", "STREET MAGICIAN", "WATCH THE TRICK", "Follow my cards and copy the pattern.", "Magnificent! A gift from my hat.", "The trick fooled you. Ha.", Q, 6f, 9f, 's', "#2F2F4F", Cu, "#C03050", 0.6f, 0.8f, 0.6f, Po));
        l.Add(D(C, "tourist_map", "TOURIST", "HELP THE TOURIST", "Excuse me, can you help me read this map?", "That is the way! Please take this souvenir.", "Hmm, I am even more lost now.", Ch, 1f, 0f, 's', "#E07F3F", Cu, "#E8E0C0", 0.3f, 0.05f, 0.4f, Po));
        l.Add(D(C, "alien_tourist", "ALIEN TOURIST", "ANSWER THE ALIEN", "Greetings, human. Explain your strange objects to me.", "Fascinating! Take this gift from my planet.", "Your answer was illogical.", Ch, 3f, 0f, 'w', "#40C080", Cu, "#606080", 0.8f, 0.6f, 0.5f, Po));
        l.Add(D(C, "flying_documents", "OFFICE WORKER", "COLLECT THE PAPERS", "My papers blew away! Can you gather them?", "Every page! Thank you. Take this.", "Half of them are gone...", O, 6f, 0f, 's', "#6F7F9F", Cu, "#F0F0F0", 0.3f, 0.02f, 0.4f, In));
        l.Add(D(C, "lost_wallet", "BYSTANDER", "RETURN THE WALLET", "Someone dropped this. Help me find who owns it.", "You found them! A reward for honesty.", "Wrong person. This is awkward.", O, 2f, 0f, 's', "#7F5F3F", Cu, "#6F4F2F", 0.15f, 0.04f, 0.1f, In));
        return l;
    }
}
