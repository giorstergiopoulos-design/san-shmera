namespace Chronikologio.Core;

/// <summary>The English interface texts. The database (saints, lives, events) stays Greek until it is translated; the interface around it speaks English.</summary>
internal static class LocalizationEn
{
    public static Dictionary<string, string> Table() => new()
    {
        ["app.title"] = "Chronikologio — Timeline Calendar",
        ["search.placeholder"] = "Search (name, saint, event)…",
        ["search.none"] = "Not found in the database.",
        ["panel.more"] = "Full text and sources…",
        ["dialog.title"] = "Chronikologio",
        ["flag.tip"] = "Country and calendar",
        ["region.gr"] = "Greece — Church of Greece",
        ["region.cy"] = "Cyprus — Church of Cyprus",
        ["menu.file"] = "File",
        ["menu.exit"] = "Exit",
        ["menu.view"] = "View",
        ["menu.today"] = "Today",
        ["menu.theme"] = "Theme",
        ["menu.calendar"] = "Calendar",
        ["menu.language"] = "Language",
        ["language.auto"] = "Automatic (the language of the country)",
        ["language.el"] = "Ελληνικά",
        ["language.en"] = "English",
        ["menu.hidemenu"] = "Hide menu",
        ["menu.help"] = "Help",
        ["menu.shortcuts"] = "Shortcuts",
        ["old.calendar"] = "old calendar",
        ["event.nophoto"] = "There is no photograph for this event yet",
        ["event.source"] = "Source",
        ["event.more"] = "more",
        ["event.none.gr"] = "There is no Greek event for this day yet.",
        ["event.none.world"] = "There is no event from abroad for this day yet.",
        ["frame.gr"] = "On this day in Greece",
        ["frame.gr.cy"] = "On this day in Cyprus",
        ["event.none.gr.cy"] = "There is no Cypriot event for this day yet.",
        ["short.history.gr.cy"] = "On this day in the history of Cyprus",
        ["frame.world"] = "On this day abroad",
        ["holiday.public"] = "Public holiday",
        ["holiday.local"] = "Local holiday",
        ["group.fasts"] = "Fasts",
        ["group.movable"] = "Movable feasts",
        ["group.national"] = "National days and holidays",
        ["group.memory"] = "Days of remembrance",
        ["group.local"] = "Local holidays of the patron saints",
        ["fast.strict"] = "strict fast",
        ["fast.lent"] = "no meat, fish, dairy or eggs; oil on Sundays and Saturdays",
        ["fast.fish"] = "no meat or dairy; fish on some days",
        ["fast.free"] = "no fast",
        ["fast.dairy"] = "dairy and eggs allowed, no meat",
        ["col.kind"] = "Kind",
        ["menu.layout"] = "Layout",
        ["layout.cards"] = "“Today” cards",
        ["layout.feed"] = "Day feed",
        ["layout.tabs"] = "Tabs",
        ["btn.today"] = "Today",
        ["card.saint"] = "Saints and names",
        ["tab.today"] = "Today",
        ["tab.calendar"] = "Calendar",
        ["tab.names"] = "Names",
        ["tab.timeline"] = "Timeline",
        ["tab.feasts"] = "Fasts and feasts",
        ["tab.easter"] = "Easter",
        ["search.names"] = "Search a name…",
        ["col.name"] = "Name",
        ["col.day"] = "Day",
        ["col.date"] = "Date",
        ["col.meaning"] = "Meaning",
        ["col.feast"] = "Feast",
        ["col.event"] = "On this day",
        ["menu.guide"] = "User guide",
        ["menu.changelog"] = "Release notes",
        ["guide.title"] = "User guide",
        ["menu.license"] = "Licence",
        ["menu.about"] = "About",
        ["help.title"] = "About",
        ["help.tab.guide"] = "User guide",
        ["help.tab.changelog"] = "Release notes",
        ["help.tab.license"] = "Licence",
        ["help.tab.sources"] = "Sources",
        ["help.license.summary"] = "Free software",
        ["help.license.disclaimer"] = "Disclaimer",
        ["help.license.content"] = "Content and images",
        ["help.license.terms"] = "Terms of the MIT licence",
        ["help.sources.data"] = "State of the data",
        ["help.sources.data.text"] = "Every entry has a state: “not verified”, “cross-checked” (the date agrees with an independent source) or “verified” (with an official source). The lives of the saints and the etymologies were written from scratch and may contain mistakes; check them against the Synaxarion of the Ecumenical Patriarchate. The texts about saints and events are in Greek for now.",
        ["changelog.title"] = "Release notes",
        ["guide.text"] =
            "WHAT IT IS\n" +
            "Chronikologio shows, for every day of the year, which saints are honoured, which names celebrate and what they mean, what happened on this day in Greek and world history, and who was born. The texts of the database are in Greek for now.\n\n" +
            "USE\n" +
            "• Click a day in the calendar. A short text appears under it and, when there is one, a picture.\n" +
            "• The button “Full text and sources” (or a double click on the short text) opens the full text with the pictures beside it.\n" +
            "• The flag at the top left changes the country: the calendar changes with it, and the language too unless you chose one in View › Language. Greece and Cyprus exist today.\n" +
            "• The search at the top right finds a name, a saint or an event, with or without accents.\n" +
            "• Major feasts are shown with a bold number and a small line under the day.\n\n" +
            "LOOK\n" +
            "Ctrl+M shows and hides the menu. Ctrl+T changes the theme. Ctrl+L changes the layout: “Today” cards, day feed or tabs. Your choice is saved.\n\n" +
            "BEWARE OF THE DATA\n" +
            "The texts were written from scratch and have not yet been verified against the official sources. The full text shows the state of each entry. Encyclopaedia links are only for more information.\n\n" +
            "SOURCES AND THANKS\n" +
            "• The Synaxarion of saint.gr (https://www.saint.gr), for the list of the saints of each day and the links to their lives. The texts of the app are written in our own words.\n" +
            "• The open calendar Orthocal (https://orthocal.info), to cross-check dates.\n" +
            "• The “On this day” columns of news sites, to cross-check the dates of events. Their texts are not copied.\n" +
            "• Wikimedia Commons, for pictures with a free licence, and Wikipedia only for more information.\n" +
            "• The official flags of Greece and Cyprus, from Wikimedia Commons.\n\n" +
            "WITH OPSIS\n" +
            "As a plugin of Opsis, the calendar replaces the built-in one and the “On this day” feed can appear in the bars of the News Crawler.",
        ["about.format"] = "Chronikologio — version {0}",
        ["help.text"] = "Ctrl+M: show or hide the menu\nCtrl+T: change theme\nCtrl+L: change layout\nCtrl+Home: today\nCtrl+F: search\nF1: user guide\n\n" +
                        "The flag at the top left changes the country, that is the calendar and, unless you chose a language, the language of the app.\n" +
                        "A double click on the short text, or the button under it, opens the full text with the sources.",
        ["short.noentries"] = "There are no entries for this day yet.",
        ["short.noothers"] = "There are no other entries for this day yet.",
        ["short.names"] = "Celebrating: ",
        ["short.births"] = "Born: ",
        ["short.history.gr"] = "On this day in Greek history",
        ["short.history.world"] = "On this day in world history",
        ["details.empty"] = "\nThe database has no entries for this day yet.",
        ["details.births"] = "\nBorn on this day",
        ["details.sources"] = "\nSources",
        ["details.unverified"] = " (not verified yet)",
        ["details.reviewed"] = " (the date was cross-checked with an independent source)",
        ["details.verified"] = " (verified)",
        ["details.synaxarion"] = "Synaxarion of saint.gr (the life of the saint): ",
        ["details.official"] = "official source: ",
        ["details.pointer"] = "for more (open encyclopaedia, not an official source): ",
        ["feed.names"] = "Celebrating today, {0}: {1}",
        ["feed.saint"] = "{0}: {1}",
        ["feed.event"] = "On this day, {0}: {1}",
        ["feed.event.year"] = "On this day, {0} {1}: {2}",
        ["feed.birth"] = "Born on this day, {0}: {1}"
    };
}
