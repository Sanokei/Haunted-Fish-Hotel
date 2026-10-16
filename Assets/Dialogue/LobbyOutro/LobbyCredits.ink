// Leaving through the front door: jib back from wherever the lobby camera is and roll the credits.
// Assign the compiled LobbyCredits.json on the Lobby outro sequence.
EXTERNAL RollCredits(seconds)
EXTERNAL Wait(seconds)

~ RollCredits(48)
~ Wait(0.5)
-> END
