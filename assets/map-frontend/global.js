const icon_by_name = {
    "clear": "00_clear",
    "rock": "01_rock",
    "spear": "02_spear",
    "boomstick": "03_explosivespear",
    "bomb": "04_explosivebomb",
    "hive": "05_hive",
    "lantern": "06_lantern",
    "lure": "07_lureplant",
    "mushroom": "08_mushroom",
    "flashbang": "09_flashbomb",
    "puffball": "10_puffball",
    "waternut": "11_bubblefruit",
    "firecrackerplant": "12_firecrackerplant",
    "bluefruit": "13_bluefruit",
    "jellyfish": "14_jellyfish",
    "bubbleweed": "15_bubbleweed",
    "slimemold": "16_slimemold",
    "slugcat": "17_slugcat",
    //## Creatures from the World File
    "green": "18_greenlizard",
    "greenlizard": "18_greenlizard",
    "pink": "19_pinklizard",
    "pinklizard": "19_pinklizard",
    "blue": "20_bluelizard",
    "bluelizard": "20_bluelizard",
    "white": "21_whitelizard",
    "whitelizard": "21_whitelizard",
    "black": "22_molelizard",
    "blacklizard": "22_molelizard",
    "yellow": "23_orangelizard",
    "yellowlizard": "23_orangelizard",
    "cyan": "24_cyanlizard",
    "cyanlizard": "24_cyanlizard",
    "red": "25_redlizard",
    "redlizard": "25_redlizard",
    "salamander": "26_salamander",
    "batfly": "27_batfly",
    "cicadaa": "28_whitecicada",
    "cicadab": "29_darkcicada",
    "cicada": "28_whitecicada", // not sure which so just picked first alphabetically
    "snail": "30_snailturtle",
    "leech": "31_redleech",
    "sea leech": "32_blueleech",
    "sealeech": "32_blueleech",
    "mimic": "33_poleplant",
    "polemimic": "33_poleplant",
    "tentacleplant": "34_monsterkelp",
    "tentacle plant": "34_monsterkelp",
    "tentacle": "34_monsterkelp",
    "scavenger": "35_scavenger",
    "vulturegrub": "36_vulturegrub",
    "vulture": "37_vulture",
    "kingvulture": "38_kingvulture",
    "king vulture": "38_kingvulture",
    "small centipede": "39_smallcentipede",
    "smallcentipede": "39_smallcentipede",
    "centipede": "40_centipede",
    "big centipede": "41_bigcentipede",
    "red centipede": "42_redcentipede",
    "redcentipede": "42_redcentipede",
    "redcenti": "42_redcentipede",
    "centiwing": "43_flyingcentipede",
    "grappleworm": "44_grappleworm",
    "tube": "44_grappleworm",
    "tubeworm": "44_grappleworm",
    "tube worm": "44_grappleworm",
    "hazer": "45_hazer",
    "lantern mouse": "46_lanternmouse",
    "mouse": "46_lanternmouse",
    "spider": "47_spider",
    "bigspider": "48_bigspider",
    "big spider": "48_bigspider",
    "spitterspider": "49_spitterspider",
    "miros bird": "50_mirosbird",
    "mirosbird": "50_mirosbird",
    "miros": "50_mirosbird",
    "bro": "51_brotherlonglegs",
    "daddy": "52_daddylonglegs",
    "deer": "53_raindeer",
    "eggbug": "54_bubblebug",
    "dropbug": "55_dropwig",
    "dropwig": "55_dropwig",
    "bigneedleworm": "56_noodlefly",
    "bigneedle": "56_noodlefly",
    "needle": "56_noodlefly",
    "smallneedleworm": "57_babynoodlefly",
    "smallneedle": "57_babynoodlefly",
    "jet fish": "58_jetfish",
    "jetfish": "58_jetfish",
    "leviathan": "59_leviathan",
    "bigeel": "59_leviathan",
    "lev": "59_leviathan",
    "randomitems": "60_randomitems_inactive",
    // MSC. excluded: SlugNPC (no predefined den), Stowaway (no icon), HunterDaddy (no icon and no predefined den), ScavengerKing (no den)
    "caramel": "73_caramel",
    "spitlizard": "73_caramel",
    "strawberry": "74_strawberry",
    "zooplizard": "74_strawberry",
    "eel": "75_eel",
    "eellizard": "75_eel",
    "terror": "76_terror",
    "terrorlonglegs": "76_terror",
    "motherspider": "77_motherspider",
    "mother spider": "77_motherspider",
    "mirosvulture": "78_mirosvulture",
    "miros vulture": "78_mirosvulture",
    "hellbug": "79_hellbug",
    "firebug": "79_hellbug",
    "scavengerelite": "80_scavengerelite",
    "scavenger elite": "80_scavengerelite",
    "elitescavenger": "80_scavengerelite",
    "elite scavenger": "80_scavengerelite",
    "elite": "80_scavengerelite",
    "inspector": "81_inspector",
    "yeek": "82_yeek",
    "bigjelly": "83_bigjelly",
    "aquacentipede": "84_aquacenti",
    "aquacenti": "84_aquacenti",
    "aquapede": "84_aquacenti",
    "jungleleech": "85_jungleleech",
    // None
    "none": "63_none",
    //## Missing stuff
    //"Garbage Worm": "00_clear",
    "garbage worm": "garbage_worm",
    //## Manually added mod stuff
    "polliwog": "polliwog",
    "scutigera": "scutigera",
    "waterspitter": "water_spitter",
    "silverlizard": "silver_lizard",
    "seeker": "hunter_seeker",
    "surfaceswimmer": "surface_swimmer",
    "sporantula": "sporantula",
    "fatfirefly": "fat_firefly",
    "snootshootnoot": "udonfly",
    "waterblob": "water_blob",
    "reaperlizard": "reaper_lizard",
    "hoverfly": "hoverfly",
    "redhorror": "red_horror",
    "noodleeater": "noodle_eater",
    "drainmite": "drain_mite",
}

const scug_icons = {
    //## Slugcat icons
    "white": "17_slugcat",
    "red": "64_hunter",
    "yellow": "65_monk",
    "gourmand": "67_gourmand",
    "artificer": "68_artificer",
    "rivulet": "69_rivulet",
    "spear": "70_spear",
    "saint": "71_saint",
};

var requestLock = {};
var lastRequest = null;

function getJsonObject(url, cb, async = true) {
    if (lastRequest != null) lastRequest.abort();
    let request = new XMLHttpRequest();
    lastRequest = request;
    requestLock = {};
    request.requestLock = requestLock;
    request.open('GET', url, async);
    request.onreadystatechange = function () {
        if (request.requestLock != requestLock && request.status != 0) {
            request.abort();
            console.log("request for " + url + " aborted!");
        }
        else if (request.readyState === 4 && request.status === 200) {
            try {
                cb(JSON.parse(request.responseText));
            } catch (err) {
                console.log(err);
            }
            if (lastRequest == request) lastRequest = null;
        }
    }
    request.send();
}
