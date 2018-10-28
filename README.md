To add the bot to the server:
https://discordapp.com/oauth2/authorize?client_id=505827616469680129&scope=bot

Temporary invite: https://discord.gg/6tQvfT

Need to add try/catch handlers so we don't blow up on exceptions

The bot needs to have all of these permissions (give it a role), because it needs to grant readers these permissions:

UseVoiceDetection
UseVoice
Speak
SendMessages
KickMembers
MuteMembers
DeafenMembers
PrioritySpeaker (256)


TODO: Figure out what's causing the throttling, and try to space out requests to avoid it.
TODO: Move to using DMs for setup and joining teams, to keep the general channel less noisy.
TODO: Add range commands for addteams/addplayers (tricky thing with the latter: how do you take the list of mentions and the list of teams? Is the parser smart enough?)
TODO: Add helper method to clean up all of the bad channels?