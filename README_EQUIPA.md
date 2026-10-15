# Hard Entry — regras da equipa

## Antes de começar
- Usar **a mesma versão do Unity: 6000.6.4f1**.
- Abrir o projeto pelo Unity Hub → *Add project from disk* → pasta clonada.

## Todos os dias
1. **Pull** (GitHub Desktop → *Fetch origin* / *Pull origin*) **antes** de abrir o Unity.
2. Trabalhar só nas tuas pastas/cenas (ver abaixo).
3. Gravar no Unity (**Ctrl+S**) e fechar o Unity antes de fazer commit.
4. **Commit** com uma frase clara (ex.: "Mapa: rua principal + iluminação").
5. **Push**.

## Quem mexe em quê
| Pessoa | Cenas | Pastas |
|---|---|---|
| Mundo (amigo) | `Assets/Scenes/Mapa_Cidade.unity` | `Assets/Mundo/` |
| Mecânicas (Leonardo) | `Assets/Scenes/Teste_Mecanicas.unity` | `Assets/Scripts/`, `Assets/Weapons/`, `Assets/Hands/` |

- O jogador, as armas, etc. ficam como **Prefabs** — o mapa só usa os prefabs.
- **Nunca** editar a cena do outro ao mesmo tempo. Se precisares, avisa primeiro.

## Ficheiros grandes
Modelos (.fbx, .blend), texturas e áudio vão por **Git LFS** (já configurado no `.gitattributes`).
