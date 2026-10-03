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
~ SetCamera("Shark")
Alright, listen up minnow. You are doing Quality Assurance.
~ SetCamera("Minnow")
What.. is it exactly I do?
~ SetCamera("Shark")
Simply, you inspect the product to see if its okay to sell.
If it is, you send them down the line to Descaling.
You can see how they look, and their weight.
// be able to access the phone through dialogue as well.
-> DONE

// ->Move_Towards_School

// = Move_Towards_School
// ~ MoveTo("strained",26,0,1.5,false)
// -> DONE