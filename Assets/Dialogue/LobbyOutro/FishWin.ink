// Played to the fish when the fish win: the fish is the winner.
// Assign the compiled FishWin.json on the Lobby outro sequence.
EXTERNAL LobbyShot(shotTag, seconds)
EXTERNAL OutroTitle(text, seconds)
EXTERNAL RollCredits(seconds)
EXTERNAL Wait(seconds)

// Frame the fish at the reception mark, then crash dolly straight into them.
~ LobbyShot("winner_fish", 0.6)
~ LobbyShot("crash_dolly", 0.9)
// The winner celebrates before the card lands.
~ LobbyShot("cheer", 1.8)
~ OutroTitle("Players Win", 2.5)
// Jib back up and away from the winner while the credits roll.
~ RollCredits(48)
~ Wait(0.5)
-> END
