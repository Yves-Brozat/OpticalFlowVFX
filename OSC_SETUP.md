# OSC Quest vers VFX

La scène `Assets/Main.unity` contient `OSC VFX Bridge`, relié au VFX principal.
En Play, il écoute sur toutes les interfaces IPv4, port UDP **9000**.
Le Quest doit envoyer à l'IPv4 du PC Unity, sur le même réseau.
Si nécessaire, autoriser la réception Unity dans le pare-feu du PC.

Les neuf adresses Quest sont préconfigurées. Les liaisons sont désactivées tant
que leurs propriétés VFX cibles ne sont pas choisies : les valeurs reçues restent
visibles dans `Last Received Values` de chaque liaison. Renseigner `Property`
puis cocher `Enabled` pour piloter le VFX.

| Adresse OSC | Argument (index à partir de 0) | Type |
| --- | --- | --- |
| `/abysses/left/position` | 0–2 | Vector3 |
| `/abysses/right/position` | 0–2 | Vector3 |
| `/abysses/left/velocity` | 0–2 | Vector3 |
| `/abysses/right/velocity` | 0–2 | Vector3 |
| `/abysses/hands/distance` | 0 | Float |
| `/abysses/left/pinch` | 0 | Float |
| `/abysses/right/pinch` | 0 | Float |
| `/abysses/left/grab` | 0 | Float |
| `/abysses/right/grab` | 0 | Float |

Exemple avec le sender Quest fourni (remplacer l'IP) :

```csharp
sender.Send(new IPEndPoint(IPAddress.Parse("192.168.1.100"), 9000),
    "/abysses/hands/distance", new float[] { 0.5f }, 1);
```

Dans `Bindings`, choisir l'adresse exacte, `First Argument`, le nom et le type
de propriété. Un Vector3 consomme trois arguments consécutifs (x, y, z).
Plusieurs bindings peuvent utiliser le même message. `Target` remplace
facultativement le VFX par défaut. Types : Float, Int (arrondi), Bool (non nul),
Vector2, Vector3 et Vector4 (aussi pour une couleur RGBA exposée en Vector4).
Chaque composante reçoit `valeur * Multiplier + Offset`, puis le clamp facultatif.
Les propriétés doivent être **exposées dans le Blackboard** : pour piloter les
attributs internes des particules, relier ces propriétés aux blocs du graph.
Le VFX actuel expose notamment `FlowFactor`, `TurbulenceIntensity`, `SourceOpacity`
et `Drag` en float. Exemple de liaison possible : distance vers `FlowFactor`.
Pour les positions/vitesses, exposer des Vector3 dans le graph et les relier
aux blocs voulus. Les coordonnées sont transmises telles quelles : adapter
l'espace et les axes côté Quest ou graph selon les conventions de la scène.

Les bindings sont appliqués uniquement à la réception, dans `LateUpdate`.
Sans nouveau message, ce composant ne modifie plus les valeurs. Un autre
contrôleur ou binder peut modifier la même propriété : attribuer une seule
source de contrôle par propriété pour éviter les conflits.

Format : un message OSC 1.0 par datagramme UDP, floats 32 bits big-endian,
compatible avec le code Quest fourni. Les entiers OSC `i` et booléens `T/F`
sont aussi acceptés (convertis en float). Bundles et autres types non pris en charge.
Messages malformés et valeurs non finies rejetés. Le travail réseau est limité
à `Max Messages Per Frame`. `Received Messages`, `Rejected Messages`,
`Last Address` et `Last Values` permettent de vérifier la réception en Play.

Pour envoyer vers le Quest, activer `Allow Sending`, renseigner son IPv4 et
son port d'écoute, puis appeler depuis le thread principal Unity :

```csharp
bridge.Send("/unity/feedback", 1f, 0.5f);
```

Aucune réponse automatique. Après changement du port d'écoute,
désactiver/réactiver le composant. Les sockets sont fermées à sa désactivation.
