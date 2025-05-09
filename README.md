# Palmer

[Pulsar](https://github.com/MelgMKW/Pulsar)を弄って少しだけ便利にした。リポジトリの名前は響きが似てるだけで大きな意味はありません。
### Authorについて
C++のプログラミングが苦手ですので、コードが読みにくいと思います。申し訳ないです...


## 必要なもの
- 総合開発環境(Visual Studio 2022)
- コードエディタ(Visual Studio Code)
- CodeWarrior Special Edition for MPC55xx/MPC56xx v2.10

CodeWarriorの入手方法については[Kamek](https://github.com/Treeki/Kamek/tree/master?tab=readme-ov-file#requirements)のリポジトリを参照すること

## 変更内容
PulsarPackCreator:
- Sceneフォルダをパック生成時に追加するように修正

Resourcesフォルダに格納されているSZSを改変することもできます。これによってパック生成時に個別でファイルを移植する手間が省けます。
- XMLの名前変換方法を修正

特定の行番号を指定して置換していた元コードを修正して、将来的なテンプレート変更にも対応できるようになりました。お好みでBase.xmlを変更しても動作します。

Engine(Code.pul):
- Inputviewer(コントローラの入力表示)の実装(設定で変更可能)
- フレームレートオプションの実装(設定で変更可能)
- スピードメーターの強化(左揃え/右揃えに対応)
- ハネアイテムつきのOTTオプションを削除
- レース中/リザルトでフレンドコード/国旗を表示する仕様を追加(表示には別途アセットが必要)
- フレンドルームの「チーム変更」を行う場面で、右下の表示が意図しないものになっている不具合を修正
- フレンドルームにおいて、レース数が意図しないものになっている不具合を修正
- InputviewerおよびSpeedometerに関して、relで定義されたカラーに依存しないよう変更(BRLYTの編集でカラーを自由に変えられるようになりました)
- その他、便利系コードを追加(詳細はMiscUI.cpp / MiscRace.cppをご覧ください)


Other:
- UIAssets.szs内のサブファイルをバニラ準拠に修正
- RaceAssets.szs内のサブファイルの座標、レイアウトファイル等を修正
- 日本語テキストの追加(進行中)

## 未実装の機能(実装できるかは不明)
- VR変動システムを変更(減少時、部屋のレートに大きな影響を受けない)
- 複数言語への対応(英語/日本語)
- カスタムゲームモードの追加(フレンドルーム限定で。Countdown, Blue Shell Showdownなど？)
- リージョンカラーの設定(Miiの服に依存させたい)
- Extended Team(拡張チーム戦)の実装
- 接続先のサーバ変更(WiiLink)については検討中です。

優先度は上にあるほど高めです。また、こちらの内容は更新予定です。

以下原文

Pulsar is a Mario Kart Wii Kamek-Based engine to create CT distributions. It comes with its own [software](../main/PulsarPackCreator/Executable) to aid in building custom distributions, and multiple quality of life features:

Core:
- Cup select expansion
- Settings that are directly modifiable in-game, including in friend rooms
- Up to four time-trial modes (150cc, 150cc feather, 200cc and 200cc feather)
- Ghost saving on all tracks and all four modes (on the SD on console and on the NAND on dolphin)
- Support for staff ghosts should the creator make them
- KO mode
- OnlineTT mode
- LEX support
- [XPF support](https://github.com/Gabriela-Orzechowska/LE-CODE-XPF) (from Gabriela)
- [USB GCN Support](https://github.com/Gabriela-Orzechowska/MKW-Cosmos/blob/main/code/System/WUP028.hpp) (from Gabriela)


UI:
- A speedometer that is flush with the game UI
- In-game crediting of track authors
- Between Races Change Combo, which has its own UI along with a randomize button
- Team selection, where the host of a room can manually set the team of each player. Moreover, team VS has been edited to play exactly as normal VS while keeping the coloured minimap icons and the scoreboard after races.
- Boot in wiimmfi directly
- Better ghost replay which allows multi ghost watching and point-of-view switching


Sound:
- BRSAR entry size patch to make all brstms loop normally
- Conditionnal channel switches; the game will only switch channel (on Koopa Cape for example) if the currently playing brstm has at least as many channels as the brsar entry requires.
- BRSTM Volume, much like it works in CTGP by editing byte 0x3F of any BRSTM
- BRSTM expansion
- Optional Music speedUp on final lap (the music is sped up when you cross the line instead of switching to the fast lap version)


Gameplay:
- 200cc support
- Ultra Mini-Turbos
- Mega TCs
- CLF78 and stebler's feather
- Support for custom CC distribution
- COOB (both kHacker35000vr's and Riidefi's versions)


Network:
- Rooms that can only be joined by people on the same pack (including the same version)
- A much faster Host Always Wins where the host selects the next track directly in the race.
- Worldwides that work as on vanilla
- The features that impact gameplay the most (200cc, UMTs, feather, mega TCs) can be turned off in the software when making your distribution. Pulsar can also be used as a base to add your own features. CTTP is such an example. The software only outputs the tracks and a config file, but the code binaries can be modified to fit your needs.


Credits:
- Treeki for [Kamek](https://github.com/Treeki/Kamek/tree/master). The engine has been ever so slightly modified to create a new output format which combines the usual 4 binaries. 
