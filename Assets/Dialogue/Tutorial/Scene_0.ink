INCLUDE ../globals.ink
INCLUDE ../monologue.ink
-> main

/*
Strained: Mad, Sympathetic, Smug, Blush, Cool, Neutral, Sad
*/

=== main ===
// Testing the functionality of this package i made. #speaker:Testing
// ~ SetCamera("Radio")
// This gets said at the same time as the above external function.
// ~ SetCamera("Sideview")
\*Yawn* #speaker:player
What time is it? #speaker:player
~ SetCamera("Radio")
G-G-G-G-GOOD MORNING AUQURIA #speaker:radio
OR SHOULD WE SAY ALMOST AFTERNOON #speaker:radio
THATS RIGHT SHARK, WE GOT OURSELVES SOME OVER. SLEEPERS. SLEEpers. sleepers. #speaker:radio
~ SetCamera("Sideview")
Oh fish, I cannot be late on my first day. #speaker:player
// be able to access the phone through dialogue as well.
-> DONE

// ->Move_Towards_School

// = Move_Towards_School
// ~ MoveTo("strained",26,0,1.5,false)
// -> DONE