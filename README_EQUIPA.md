# Hard Entry — regras da equipa

Jogo VR para **Meta Quest**, render pipeline **URP** (o HDRP não funciona no Quest).

## Antes de começar
- Usar **a mesma versão do Unity: 6000.6.4f1**.
- Abrir o projeto pelo Unity Hub → *Add project from disk* → pasta clonada.

## Todos os dias
1. **Commit** do que tens feito **antes** de fazer Pull (nunca aceitar "Stash" no GitHub Desktop).
2. **Pull** (GitHub Desktop → *Fetch origin* / *Pull origin*) com o Unity **fechado**.
3. Trabalhar só nas tuas pastas/cenas (ver abaixo).
4. Gravar no Unity (**Ctrl+S**) e fechar o Unity antes de fazer commit.
5. **Commit** com uma frase clara (ex.: "Mapa: rua principal + iluminação") e **Push**.

## Pastas
```
Assets/
  _Leonardo_Mecanicas/      ← Leonardo
     Arsenal/               armas (G17...), GripData
     Maos/                  mãos e luvas
     Equipamento/           colete, bolsas
     Audio/
     Scripts/               todo o código (Weapons, Gear, Editor...)
     Cenas/                 SampleScene (teste das mecânicas)
  _Simao_Mundo/             ← Simão
     Cenas/                 Mapa.unity, Mapa_Simao.unity
     Terrenos/              TerrainData / terrenos
     Agua/                  shaders e materiais de água
     Modelos/  Materiais/  Texturas/  Prefabs/  Iluminacao/
  _Terceiros/               coisas da Asset Store / downloads (não editar)
     ALP_Assets/  Nokobot/  TerrainSampleAssets/
  Samples/ TextMesh Pro/ XR/ XRI/ Settings/ InteractionSDK/   ← da Unity/Meta, NÃO MEXER
```
- Cada um só cria/edita dentro da sua pasta. Coisas novas da Asset Store vão para `_Terceiros/`.
- **Nunca** editar a cena do outro. O mapa usa os prefabs das mecânicas (jogador, armas).
- Mover ficheiros **só dentro do Unity** (arrastar no Project), nunca no Explorador — senão partem as referências.

## O que podemos usar (Quest + URP)
| ✅ Pode | ❌ Não pode / evitar |
|---|---|
| Shaders URP: Lit, Simple Lit, Unlit, Shader Graph (alvo URP) | Qualquer coisa HDRP (materiais HDRP ficam cor-de-rosa) |
| Luz **baked** (lightmaps), Light Probes, Reflection Probes baked | Muitas luzes em tempo real com sombras (no máximo o sol) |
| Particle System (partículas normais) | VFX Graph, volumetric fog, SSR, SSAO pesado |
| Texturas até 2048, compressão ASTC | Texturas 4K / 8K, demos de PC (ex.: TerrainDemoScene, 4,6 GB) |
| LODs, Occlusion Culling, objetos estáticos | Terrenos enormes com muitas camadas (máx. ~4 camadas) |
| Pós-processamento leve (cor, bloom fraco) | Pós-processamento pesado, refração/transparência em ecrã inteiro |

### Água (URP)
- Fazer um **Shader Graph URP** simples: 2 normal maps a deslizar + cor + fresnel; reflexo por **Reflection Probe** (baked).
- Evitar "Depth/Opaque Texture" (refração e espuma pela profundidade) — custa muito no Quest.
- Plano da água com poucos polígonos; se mexer as ondas, fazê-lo no vertex shader.
- Alternativa paga: asset de água para URP com suporte **mobile/VR** (confirmar na página do asset).

## Ficheiros grandes
Modelos (.fbx, .blend), texturas e áudio vão por **Git LFS** (já configurado no `.gitattributes`).
Terrenos (`TerrainData`) são binários — já estão marcados no `.gitattributes`.
