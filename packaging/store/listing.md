# Microsoft Store listing

Copy these into Partner Center > Yappity > Submission. Screenshots are the `screenshot-*.png` files in
this folder, made by `tools/make-store-shots.py`.

## Properties

- **Category:** Productivity
- **Privacy policy URL:** https://github.com/paulablaza/yappity/blob/main/PRIVACY.md
- **Website:** https://github.com/paulablaza/yappity
- **Support contact info:** https://github.com/paulablaza/yappity/issues
- **This product accesses, collects or transmits personal information:** Yes (audio goes to Groq)
- **System requirements:** Microphone required. Keyboard required.

## Age ratings (IARC questionnaire)

- Category: **Productivity / Utility** (not a game)
- Violence, sex, language, drugs, gambling: **No** to all
- Users can talk to each other or share content with other users: **No**
- Shares the user's location: **No**
- Lets users buy digital goods: **No**
- Unrestricted internet browsing: **No**

## Listing (English)

**Product name:** Yappity

**Short description:**

Free voice typing for Windows. Tap Right Ctrl, talk, tap it again, and your words get typed wherever
your cursor is. Runs on your own free Groq API key.

**Description:**

Yappity is free voice typing for Windows. Tap Right Ctrl, talk, then tap it again. Whatever you said
shows up right where your cursor is, in any app.

It's fast because the heavy lifting happens on Groq. Whisper turns your voice into text in about a
second, then a small model takes out the um's, uh's and you know's and fixes the punctuation. It
never rewrites your sentences, never answers a question you dictate and never translates. If you say
"at 3, no wait, at 4", you get "at 4".

Yappity is tiny. It sits in your tray using about 2 MB of memory and wakes up when you press your
shortcut.

You need a free Groq API key, which takes about a minute to get at console.groq.com/keys. Groq's
free plan covers about 2,000 recordings a day. There's no Yappity account, no subscription and no
ads.

Privacy: your audio goes from your PC straight to Groq and nowhere else. There are no analytics and
no server of mine in between. Your key is encrypted on your PC, and your history and recordings stay
on your PC too.

Yappity is open source under the MIT license.

**Product features:**

1. Free voice typing in any app, with your own free Groq API key
2. About a second from when you stop talking to when the text shows up
3. Cleans out filler words and fixes punctuation without changing what you said
4. Four styles: Raw, Clean up, Clean up and format, and Organize
5. English, Tagalog and Taglish
6. Every recording is saved, so you never lose a yap
7. Tap or hold any shortcut you like, Right Ctrl by default, Esc to cancel
8. Recordings up to 30 minutes
9. A little bubble shows while you talk and never steals focus
10. Light and dark mode
11. No account, no ads, no analytics

**Search terms:** voice typing, dictation, speech to text, whisper, transcription, voice to text, groq

**Copyright and trademark info:** © 2026 Paul Martin Ablaza

**What's new in this version:** First Microsoft Store release.

## Release notes for later

- Bump `App.Version` in `src/App.cs` before every upload. The Store rejects a version it has seen.
- Run `build-msix.ps1` and upload `bin\Yappity-<version>-x64.msix` under Packages.
