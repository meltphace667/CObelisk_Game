Obelisk — sélection Map V1
================================

Ces 17 fichiers sont renommés pour correspondre directement aux IDs de rooms de la map V1.
Tu peux les importer dans Unity et les mettre en Sprite (2D and UI), puis les assigner aux rooms du BackgroundManager.

Logique de navigation :
Ob_01 = start
Ob_02 = obélisque proche / source musicale
PRA_01 = carrefour central
LAC_A1 -> LAC_A2 -> LAC_01 = chemin lac principal
FOR_L1 -> FOR_L2 -> LAC_01 = détour secondaire vers le lac
SIL_01 -> SIL_02 -> SIL_03 = après-lac / silence / réceptacle
FOR_01 -> FOR_02 -> CHA_FAR -> CHA_NEAR -> CHA_INT_01 -> CHA_INT_02 = route château / cadre

Notes :
- Je n’ai pas choisi seulement les images les plus propres : certaines images floues/basses résolution sont gardées quand elles servent l’ambiance rêve/point-and-click.
- SIL_03 est volontairement le bloc blanc : il donne un objet/réceptacle lisible.
- CHA_INT_02 est volontairement la salle avec un grand cadre central, pour ton moment de révélation.

Mapping :

Ob_01 -> Ob_01(2).jpg
    Départ près de l’axe de l’obélisque, mais pas encore collé à lui.

Ob_02 -> DSCN8002.JPG
    Obélisque plein cadre, source sonore la plus proche.

PRA_01 -> PRA_01.jpg
    Grande prairie neutre servant de carrefour central.

LAC_A1 -> 24010858.400x300(1).jpg
    Première entrée vers le lac : eau visible mais encore ouverte.

LAC_A2 -> 3a316186fbdeb1ccf7c19db7ef89b506-1759419120.png
    Approche plus aquatique, reflet et horizon plus enveloppants.

LAC_01 -> csm_Tournay_1_8aa1de86a5.jpg
    Bord du lac principal, calme et lourd, juste avant le silence.

FOR_L1 -> eyJidWNrZXQiOiJhc3NldHMuYWxsdHJhaWxzLmNvbSIsImtleSI6InVwbG9hZHMvcGhvdG8vaW1hZ2UvNDQzMTMxMTQvYTVkOTFhYmFjZmI3ZDYyNTI3ODA3NzBjZmE5ZTgyYzQuanBnIiwiZWRpdHMiOnsidG9Gb3JtYXQiOiJ3ZWJwIiwicmVzaXplIjp.png
    Détour latéral en forêt, chemin lisible mais moins frontal.

FOR_L2 -> Photo-Tournay-Solvay.jpg
    Passage/bridge forestier avant de retomber vers le lac.

SIL_01 -> 5932435.400x300(1).jpg
    Après-lac sombre, eau stagnante, baisse de repères.

SIL_02 -> 0 (5).png
    Zone sèche et vide, rupture naturelle avant le réceptacle.

SIL_03 -> 22377704.400x300(1).jpg
    Réceptacle visuel clair : bloc blanc isolé près de l’eau.

FOR_01 -> 0 (4).png
    Entrée de forêt vers la branche château, chemin droit et clair.

FOR_02 -> 6065253-8cc34455(1).jpg
    Forêt plus profonde, plus basse résolution et plus rêveuse.

CHA_FAR -> pc060085(2).jpg
    Château très loin au bout de l’axe, bon palier d’approche.

CHA_NEAR -> chc3a2teau-de-la-hulpe-belgium-ct84_l.jpeg
    Façade proche, architecture/haies visibles, arrivée au château.

CHA_INT_01 -> chateau-de-la-hulpe-feestzaal-te-la-hulpe-house-of-weddings-5-5cb5cfe58ff4d.jpg
    Premier intérieur clair, transition entrée/hall.

CHA_INT_02 -> DSC_3142-scaled.jpg
    Salle avec grand cadre central au-dessus de la cheminée : room du cadre important.
