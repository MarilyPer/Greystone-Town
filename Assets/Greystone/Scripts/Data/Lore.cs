// =====================================================================================
//  Lore.cs — Περιεχόμενο πινακίδων πληροφόρησης και πηγές
//  ---------------------------------------------------------------------------------
//  Το περιεχόμενο (κείμενα) είναι ΔΙΑΧΩΡΙΣΜΕΝΟ από τον κώδικα λογικής: οι πινακίδες
//  (InfoBoard) και το HUD διαβάζουν από εδώ με ένα κλειδί. Αυτό διευκολύνει τη
//  διόρθωση/μετάφραση χωρίς αλλαγές στα υπόλοιπα scripts (Separation of Concerns).
//
//  Κάθε λήμμα έχει: τίτλο, σύντομο κείμενο (πάνω στην πινακίδα στον κόσμο), αναλυτικό
//  κείμενο (παράθυρο HUD) και λίστα πηγών (εμφανίζονται μέσα στον εικονικό κόσμο).
// =====================================================================================
using System.Collections.Generic;

namespace Greystone
{
    public class LoreEntry
    {
        public string key;
        public string title;
        public string shortText;
        public string body;
        public string[] sources;
    }

    public static class Lore
    {
        static Dictionary<string, LoreEntry> _entries;

        public static LoreEntry Get(string key)
        {
            if (_entries == null) Build();
            LoreEntry e;
            return _entries.TryGetValue(key, out e) ? e : null;
        }

        public static IEnumerable<LoreEntry> All
        {
            get { if (_entries == null) Build(); return _entries.Values; }
        }

        static void Add(string key, string title, string shortText, string body, params string[] sources)
        {
            _entries[key] = new LoreEntry { key = key, title = title, shortText = shortText, body = body, sources = sources };
        }

        static void Build()
        {
            _entries = new Dictionary<string, LoreEntry>();

            Add("welcome",
                "Καλώς ήρθατε στο Greystone",
                "Ένα χωριό της ύστερης μεσαιωνικής Αγγλίας (περ. 1400 μ.Χ.), εμπνευσμένο από τα χωριά των Cotswolds.",
                "Το Greystone είναι φανταστικό χωριό της ύστερης μεσαιωνικής Αγγλίας (περίπου 1400 μ.Χ.), εμπνευσμένο από " +
                "τα χωριά των λόφων Cotswolds, όπως το Castle Combe και το Bibury.\n\n" +
                "Τα σπίτια χτίζονται από τον τοπικό ιουρασικό ωολιθικό ασβεστόλιθο («Cotswold stone»), του οποίου το χρώμα " +
                "κυμαίνεται από μελί έως γκριζωπό, ενώ οι στέγες καλύπτονται με πλάκες από την ίδια πέτρα. " +
                "Ο πλούτος της περιοχής τον ύστερο Μεσαίωνα προερχόταν κυρίως από το εμπόριο μαλλιού.\n\n" +
                "Περιηγηθείτε ελεύθερα: μιλήστε με τους κατοίκους, χτυπήστε την καμπάνα, λύστε το φρένο του ανεμόμυλου, " +
                "αντλήστε νερό από το πηγάδι και διαβάστε τις πινακίδες. Πατήστε H για βοήθεια και M για χάρτη.",
                "Wikipedia — «Cotswolds»: https://en.wikipedia.org/wiki/Cotswolds",
                "Wikipedia — «Cotswold stone»: https://en.wikipedia.org/wiki/Cotswold_stone",
                "Wikipedia — «Castle Combe»: https://en.wikipedia.org/wiki/Castle_Combe",
                "Wikipedia — «Bibury»: https://en.wikipedia.org/wiki/Bibury");

            Add("church",
                "Ο ενοριακός ναός και η καμπάνα",
                "Η καμπάνα σημαίνει τις ώρες 07:00–21:00. Τραβήξτε το σχοινί για να τη χτυπήσετε.",
                "Ο ενοριακός ναός ήταν το κέντρο της ζωής του μεσαιωνικού χωριού: τόπος λατρείας, συνάθροισης και ανακοινώσεων.\n\n" +
                "Οι καμπάνες χυτεύονταν από «μέταλλο καμπάνας», κράμα χαλκού και κασσίτερου σε αναλογία περίπου 4 προς 1. " +
                "Ο ήχος τους αποτελείται από πολλά «μερικά» (hum, prime, tierce, quint, nominal) που σβήνουν με διαφορετικό ρυθμό — " +
                "ακριβώς έτσι συντίθεται και ο ήχος της καμπάνας του Greystone.\n\n" +
                "Χτυπούσαν για τις ακολουθίες, για γεγονότα της κοινότητας αλλά και ως συναγερμός.",
                "Wikipedia — «Church bell»: https://en.wikipedia.org/wiki/Church_bell",
                "Wikipedia — «Bell metal»: https://en.wikipedia.org/wiki/Bell_metal",
                "Wikipedia — «Strike tone»: https://en.wikipedia.org/wiki/Strike_tone",
                "Wikipedia — «Parish church»: https://en.wikipedia.org/wiki/Parish_church");

            Add("clock",
                "Το ρολόι του πύργου",
                "Τα πρώτα μηχανικά ρολόγια εμφανίστηκαν στην Ευρώπη στα τέλη του 13ου αιώνα.",
                "Τα πρώτα μηχανικά ρολόγια με διαφυγή τύπου verge εμφανίστηκαν σε εκκλησίες και μοναστήρια της Ευρώπης " +
                "στα τέλη του 13ου αιώνα. Το ρολόι του καθεδρικού ναού του Salisbury (περ. 1386) θεωρείται από τα αρχαιότερα " +
                "ρολόγια που λειτουργούν ακόμη.\n\n" +
                "Πολλά πρώιμα ρολόγια δεν είχαν καν πλάκα με δείκτες: απλώς χτυπούσαν τις ώρες σε μια καμπάνα. " +
                "Το ρολόι του Greystone δείχνει την ώρα του εικονικού κόσμου και κινείται σε πραγματικό χρόνο.",
                "Wikipedia — «Salisbury Cathedral clock»: https://en.wikipedia.org/wiki/Salisbury_Cathedral_clock",
                "Wikipedia — «Verge escapement»: https://en.wikipedia.org/wiki/Verge_escapement",
                "Wikipedia — «Clock tower»: https://en.wikipedia.org/wiki/Clock_tower");

            Add("well",
                "Το πηγάδι του χωριού",
                "Ο κουβάς ανεβαίνει με βαρούλκο — εφαρμογή του «τροχού και άξονα».",
                "Τα κοινόχρηστα πηγάδια ήταν η κύρια πηγή πόσιμου νερού του χωριού και σημείο συνάντησης των κατοίκων.\n\n" +
                "Ο κουβάς ανεβαίνει με βαρούλκο (windlass): ένα οριζόντιο τύμπανο με μανιβέλα γύρω από το οποίο τυλίγεται " +
                "το σχοινί. Επειδή η μανιβέλα διαγράφει μεγαλύτερο κύκλο από το τύμπανο, απαιτείται μικρότερη δύναμη — " +
                "μια εφαρμογή της απλής μηχανής «τροχός και άξονας».",
                "Wikipedia — «Water well»: https://en.wikipedia.org/wiki/Water_well",
                "Wikipedia — «Windlass»: https://en.wikipedia.org/wiki/Windlass",
                "Wikipedia — «Wheel and axle»: https://en.wikipedia.org/wiki/Wheel_and_axle");

            Add("market",
                "Η εβδομαδιαία αγορά",
                "Το δικαίωμα αγοράς παραχωρούνταν με βασιλικό χάρτη (market charter).",
                "Στη μεσαιωνική Αγγλία το δικαίωμα διεξαγωγής εβδομαδιαίας αγοράς ή ετήσιου πανηγυριού παραχωρούνταν " +
                "συνήθως με βασιλικό χάρτη (market charter).\n\n" +
                "Στην πλατεία υψωνόταν συχνά ένας λίθινος «σταυρός της αγοράς» (market cross), όπως αυτός στα ανατολικά της " +
                "πλατείας μας. Κωμοπόλεις των Cotswolds, όπως το Chipping Campden, οφείλουν το όνομά τους στην αγορά: " +
                "«chipping» προέρχεται από το παλαιοαγγλικό cēping, που σημαίνει αγορά.",
                "Wikipedia — «Market town»: https://en.wikipedia.org/wiki/Market_town",
                "Wikipedia — «Market cross»: https://en.wikipedia.org/wiki/Market_cross",
                "Wikipedia — «Chipping Campden»: https://en.wikipedia.org/wiki/Chipping_Campden");

            Add("forge",
                "Το σιδηρουργείο",
                "Πατήστε E στο φυσερό για να δυναμώσει η φωτιά.",
                "Ο σιδεράς θέρμαινε το σίδερο στο καμίνι με κάρβουνο, ενώ το φυσερό (bellows) τροφοδοτούσε τη φωτιά με αέρα " +
                "ώστε να ανεβαίνει η θερμοκρασία. Ο τεχνίτης έκρινε τη θερμοκρασία από το χρώμα του μετάλλου " +
                "(σκούρο κόκκινο → πορτοκαλί → κίτρινο).\n\n" +
                "Στο αμόνι σφυρηλατούσε εργαλεία, καρφιά, πέταλα, μεντεσέδες και μέρη για τα κάρα — ήταν από τους " +
                "πιο απαραίτητους τεχνίτες κάθε κοινότητας.",
                "Wikipedia — «Blacksmith»: https://en.wikipedia.org/wiki/Blacksmith",
                "Wikipedia — «Forge»: https://en.wikipedia.org/wiki/Forge",
                "Wikipedia — «Bellows»: https://en.wikipedia.org/wiki/Bellows");

            Add("windmill",
                "Ο ανεμόμυλος",
                "Ο τρούλος στρέφεται προς τον άνεμο. Ο μοχλός ελέγχει το φρένο.",
                "Οι ανεμόμυλοι εμφανίστηκαν στη βορειοδυτική Ευρώπη τον 12ο αιώνα. Οι πρώτοι ήταν «στυλόμυλοι» (post mills), " +
                "όπου ολόκληρο το σώμα του μύλου περιστρεφόταν για να κοιτάξει τον άνεμο. Στους πυργόμυλους (tower mills), " +
                "που εμφανίστηκαν από τα τέλη του 13ου αιώνα, περιστρέφεται μόνο ο τρούλος (cap) με τα πτερύγια.\n\n" +
                "Ο μύλος άλεθε σιτάρι και κριθάρι σε αλεύρι. Στο Greystone ο τρούλος ακολουθεί αργά την κατεύθυνση του ανέμου " +
                "και τα πτερύγια γυρίζουν ανάλογα με την ένταση του ανέμου (δυνατότερα όταν έχει καιρό).",
                "Wikipedia — «Windmill»: https://en.wikipedia.org/wiki/Windmill",
                "Encyclopaedia Britannica — «Windmill»: https://www.britannica.com/technology/windmill");

            Add("fields",
                "Οι αγροί ανοιχτού τύπου",
                "Λωρίδες καλλιέργειας και αμειψισπορά τριών αγρών.",
                "Στο σύστημα ανοιχτών αγρών (open-field system) η γη του χωριού χωριζόταν σε στενές λωρίδες που " +
                "καλλιεργούσαν διαφορετικές οικογένειες — γι' αυτό τα χωράφια μας έχουν λωρίδες με ελαφρώς διαφορετικό χρώμα.\n\n" +
                "Εφαρμοζόταν αμειψισπορά τριών αγρών: ένας αγρός με χειμερινό σιτάρι, ένας με ανοιξιάτικο κριθάρι ή όσπρια, " +
                "και ένας σε αγρανάπαυση ώστε να ξεκουραστεί το έδαφος. Εδώ βλέπετε το σιτάρι (δυτικά) και το κριθάρι (ανατολικά).",
                "Wikipedia — «Open-field system»: https://en.wikipedia.org/wiki/Open-field_system",
                "Wikipedia — «Crop rotation»: https://en.wikipedia.org/wiki/Crop_rotation");

            Add("sheep",
                "Πρόβατα και μαλλί",
                "Η φυλή Cotswold ονομαζόταν «Cotswold Lion».",
                "Τον ύστερο Μεσαίωνα το αγγλικό μαλλί ήταν το σημαντικότερο εξαγώγιμο προϊόν της χώρας, και τα Cotswolds " +
                "από τις πλουσιότερες περιοχές παραγωγής του.\n\n" +
                "Η φυλή Cotswold, με το μακρύ σγουρό μαλλί της, ονομαζόταν «Cotswold Lion». Με τα κέρδη από το μαλλί οι έμποροι " +
                "χρηματοδότησαν μεγαλοπρεπείς ναούς, γνωστούς ως «εκκλησίες του μαλλιού» (wool churches).\n\n" +
                "Πλησιάστε τα πρόβατα προσεκτικά — αν τρέξετε, θα απομακρυνθούν.",
                "Wikipedia — «Cotswold sheep»: https://en.wikipedia.org/wiki/Cotswold_sheep",
                "Wikipedia — «Wool church»: https://en.wikipedia.org/wiki/Wool_church");

            Add("bridge",
                "Η γέφυρα και τα διόδια (pontage)",
                "Η μπάρα των διοδίων ανοίγει αυτόματα όταν πλησιάζετε.",
                "Το pontage ήταν τέλος που επέτρεπε το στέμμα να εισπράττεται από όσους διέσχιζαν μια γέφυρα, με σκοπό τη " +
                "συντήρηση ή την ανοικοδόμησή της. Με τον καιρό πολλές ξύλινες γέφυρες αντικαταστάθηκαν από λίθινες.\n\n" +
                "Η μπάρα της γέφυρας του Greystone διαθέτει «αισθητήρα»: ανοίγει όταν ο επισκέπτης μπει στη ζώνη " +
                "ελέγχου και κλείνει μόλις απομακρυνθεί.",
                "Wikipedia — «Pontage»: https://en.wikipedia.org/wiki/Pontage",
                "Wikipedia — «Toll bridge»: https://en.wikipedia.org/wiki/Toll_bridge");

            Add("tavern",
                "Η ταβέρνα και το ale-stake",
                "Ένα κοντάρι με κλαδιά έξω από την πόρτα σήμαινε: «έχουμε φρέσκια μπύρα».",
                "Οι μεσαιωνικές αγγλικές ταβέρνες (alehouses) ειδοποιούσαν ότι είχαν φρέσκια μπύρα κρεμώντας έξω από την πόρτα " +
                "ένα κοντάρι με κλαδιά, το ale-stake. Η μπύρα (ale), φτιαγμένη χωρίς λυκίσκο, ήταν καθημερινό ρόφημα.\n\n" +
                "Η ταβέρνα του Greystone είναι το μόνο κτίριο που μπορείτε να επισκεφθείτε: μπείτε και ζεσταθείτε στο τζάκι.",
                "Wikipedia — «Ale-stake»: https://en.wikipedia.org/wiki/Ale-stake",
                "Wikipedia — «Ale»: https://en.wikipedia.org/wiki/Ale");

            Add("night",
                "Φανάρια και νυχτοφυλακή",
                "Τη νύχτα ο νυχτοφύλακας περιπολεί με το φανάρι του.",
                "Οι μεσαιωνικοί δρόμοι ήταν ουσιαστικά σκοτεινοί. Στο Λονδίνο, το 1417, ο δήμαρχος Henry Barton διέταξε " +
                "να κρεμιούνται φανάρια έξω από τα σπίτια τις χειμωνιάτικες νύχτες — γι' αυτό τα σπίτια του Greystone έχουν " +
                "φανάρια δίπλα στις πόρτες, που ανάβουν αυτόματα με τη δύση του ήλιου.\n\n" +
                "Το Καταστατικό του Winchester (1285) όριζε νυχτερινή φρουρά (watch and ward) στις πόλεις. Τη νύχτα, ο " +
                "νυχτοφύλακας του Greystone περιπολεί τους δρόμους κρατώντας φανάρι.",
                "Wikipedia — «Street light» (History): https://en.wikipedia.org/wiki/Street_light",
                "Wikipedia — «Statute of Winchester»: https://en.wikipedia.org/wiki/Statute_of_Winchester");

            Add("hurdy",
                "Η λύρα με τροχό (hurdy-gurdy)",
                "E: παίξτε / σταματήστε. Ροδέλα ποντικιού: ένταση.",
                "Έγχορδο όργανο όπου ένας τροχός, που γυρίζει με μανιβέλα, τρίβει τις χορδές σαν δοξάρι. Η μελωδία παίζεται με " +
                "πλήκτρα που πιέζουν τις χορδές, ενώ οι «μπουρδόνοι» (drones) ηχούν συνεχώς. Πρόγονός του ήταν το organistrum " +
                "(11ος–12ος αιώνας).\n\n" +
                "Η μελωδία που ακούτε είναι πρωτότυπη σύνθεση σε δώριο τρόπο, πάνω σε μπουρδόνι Ρε–Λα, και παράγεται " +
                "διαδικαστικά από τον κώδικα (Synth.cs).",
                "Wikipedia — «Hurdy-gurdy»: https://en.wikipedia.org/wiki/Hurdy-gurdy",
                "Wikipedia — «Dorian mode»: https://en.wikipedia.org/wiki/Dorian_mode");

            Add("sky",
                "Ο ήλιος πάνω από τα Cotswolds",
                "Η θέση του ήλιου υπολογίζεται αστρονομικά για πλάτος 51,8° Β.",
                "Η πορεία του ήλιου στον ουρανό του Greystone δεν είναι αυθαίρετη: υπολογίζεται με τον τύπο του ηλιακού ύψους " +
                "sin(a) = sinφ·sinδ + cosφ·cosδ·cosH, για γεωγραφικό πλάτος φ = 51,8° Β (Cotswolds) και ηλιακή απόκλιση δ = 12° " +
                "(αρχές Μαΐου). Έτσι ο ήλιος ανατέλλει περίπου στις 5:00 στα βορειοανατολικά, μεσουρανεί νότια σε ύψος ≈50° " +
                "και δύει περίπου στις 19:00 στα βορειοδυτικά.",
                "Wikipedia — «Solar zenith angle»: https://en.wikipedia.org/wiki/Solar_zenith_angle",
                "Wikipedia — «Position of the Sun»: https://en.wikipedia.org/wiki/Position_of_the_Sun");

            Add("credits",
                "Πηγές και συντελεστές",
                "Όλη η γεωμετρία, οι υφές, οι ήχοι και η μουσική παράγονται διαδικαστικά από κώδικα.",
                "Μηχανή: Unity (Built-in Render Pipeline), γλώσσα C# και shaders σε HLSL/Cg.\n\n" +
                "Κανένα εξωτερικό asset δεν χρησιμοποιείται: η γεωμετρία (Geo.cs), οι υφές (shaders με θόρυβο), οι ήχοι και η " +
                "μουσική (Synth.cs) δημιουργούνται από κώδικα κατά τη φόρτωση.\n\n" +
                "Τεχνικές βελτιστοποίησης: GPU instancing βλάστησης σε τμήματα (chunks) με frustum culling και LOD απόστασης, " +
                "static batching και LOD Groups στα κτίρια, απόσταση αποκοπής ανά layer, Update Manager με αραίωση " +
                "ενημερώσεων (time-slicing), object pooling ήχων, LOD φώτων, κοινά υλικά (Flyweight), καθολικές μεταβλητές shader.\n\n" +
                "Αλγόριθμοι: A* σε γράφο δρόμων (RoadNetwork.cs), σμήνος πουλιών τύπου boids, μηχανές καταστάσεων για τους χαρακτήρες.",
                "Unity Manual — «GPU instancing»: https://docs.unity3d.com/Manual/GPUInstancing.html",
                "Unity Manual — «Static batching»: https://docs.unity3d.com/Manual/static-batching.html",
                "Unity Manual — «Level of Detail (LOD)»: https://docs.unity3d.com/Manual/LevelOfDetail.html",
                "Unity Blog — «10000 Update() calls»: https://blog.unity.com/engine-platform/10000-update-calls",
                "Wikipedia — «A* search algorithm»: https://en.wikipedia.org/wiki/A*_search_algorithm",
                "C. Reynolds (1987), «Flocks, herds and schools» (Boids): https://www.red3d.com/cwr/boids/",
                "K. Perlin (1985), «An image synthesizer» — θόρυβος Perlin: https://en.wikipedia.org/wiki/Perlin_noise");
        }
    }
}
