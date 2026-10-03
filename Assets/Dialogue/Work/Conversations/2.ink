INCLUDE ../../globals.ink
INCLUDE ../../monologue.ink
-> main

=== main ===
~ SetCamera("Productview")
...
~ SetCamera("Minnowview")
um.
~SetCamera("Workview")
...
*[yes]
~ SetCamera("Minnowview")
ok...
~SetCamera("Workview")
-> DONE
*[no]
~ SetCamera("Minnowview")
ok...
~SetCamera("Workview")
-> DONE