# Yappity privacy policy

Last updated: November 8, 2026

Yappity is a free voice typing app for Windows made by Paul Martin Ablaza. This page explains what
happens to your voice and your data when you use it. The short version: Yappity has no account, no
analytics, no ads and no server of mine. Your audio goes from your PC to Groq and nowhere else.

## What Yappity sends, and where

When you record, Yappity sends two things to [Groq](https://groq.com), using the Groq API key you
put in yourself:

1. **Your recording**, to Groq's speech to text model (Whisper), which sends back the text.
2. **That text**, to a language model on Groq that removes filler words and fixes punctuation.
   If you pick the Raw style, this step is skipped.

If you add words to the vocabulary list in Settings, those words go along with the recording so
Whisper spells them right.

That's it. Yappity doesn't send anything to me or to anyone else, and it doesn't send anything
when you're not recording. Groq handles your audio and text under its own terms and privacy
policy, at [groq.com/privacy-policy](https://groq.com/privacy-policy). Your Groq account is
between you and Groq.

## What stays on your PC

- **Your Groq API key**, encrypted with Windows DPAPI so only your Windows account can read it.
- **Your settings**, like your shortcut, style and language.
- **History**: the last 200 things you said, as text.
- **Recordings**: saved before they're sent, so a yap is never lost if something fails. They get
  deleted on their own after the number of days you pick in Settings (7 by default). Recordings
  that failed to send are kept for at least 30 days so you can send them again.
- **A small log file** with errors, to help fix problems. It doesn't contain your recordings.

All of this lives in your Windows user folder. You can delete any item from History, and removing
Yappity from Windows Settings > Apps removes the Store version and its data.

## Microphone

Yappity only uses your microphone while you're recording, after you press your shortcut. The bubble
at the top of your screen shows when it's listening. You can turn off microphone access any time in
Windows Settings > Privacy & security > Microphone.

## What Yappity doesn't do

- No account or sign in.
- No analytics, tracking, crash reporting or ads.
- No selling or sharing of your data. There's nothing for me to sell, because none of it reaches me.

## Kids

Yappity isn't made for children under 13, and it doesn't knowingly collect anything from them.
It doesn't collect anything from anyone, really.

## Changes

If this policy changes, the new version goes up on this page with a new date. The full history is
in this repo's commits.

## Contact

Questions or problems: open an issue at
[github.com/paulablaza/yappity/issues](https://github.com/paulablaza/yappity/issues).
