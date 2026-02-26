汁仕分表 PDF で日本語を表示するには、このフォルダに日本語対応の .ttf フォントを 1 つ配置してください。

配置方法:
  1. 日本語 TrueType フォント（.ttf）を入手します。
     例: IPAexゴシック (https://moji.or.jp/ipafont/) の ipaexg00401.zip を解凍し、
         ipaexg.ttf をこの Fonts フォルダにコピーします。
  2. 次のいずれかのファイル名で配置してください。
     - ipaexg.ttf （IPAexゴシック・推奨）
     - ipag.ttf   （IPAゴシック）
     - JapaneseFont.ttf （任意の日本語 .ttf をリネームした場合）
  3. プロジェクトをビルドすると、フォントは出力先にコピーされ、PDF で正しく日本語が表示されます。

フォントを配置しない場合、Windows の yugothui.ttf があればそれを使います。
どちらもない場合は「日本語フォントが見つかりません」となり、PDF 印刷時にエラーになります（Arial は日本語非対応のため使用しません）。
