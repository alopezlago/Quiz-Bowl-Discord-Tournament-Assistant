To add the bot to the server:
https://discordapp.com/oauth2/authorize?client_id=505827616469680129&scope=bot

Temporary invite: https://discord.gg/6tQvfT

The bot needs to have all of these permissions (give it a role), because it needs to grant readers these permissions:

Use Voice Activity (UseVoiceDetection)
Manage Roles (ManageRoles)
Kick Members (KickMembers)
Read Text Channels & See Voice Channels
Send Messages (SendMessages)
Read Message History
Add Reactions (AddReactions)
Connect (UseVoice)
Speak (Speak)
Mute Members (MuteMembers)
Defean Members (DeafenMembers)
Move Members (MoveMembers)
Priority Speaker (PrioritySpeaker, 256)

These may be needed:
Manage Emojis (ManageEmojis)


Starting a tournament:
  1. Create your tournament by adding yourself as a TD to a tournament.
    !addTD @my_user <My tournament name>
  2. Start the setup phase with !setup
    !setup
  3. Add all of the readers based on their mentions.
     @Reader1  @Reader2
  4. Set the number of round robins to play with
    3
  5. Add all of the teams in the tournament
    Team 1, Team 2, Team 3
  6. Players will join their team by clicking on the reaction that matches the team.
  7. Start the tournament with !start.
    !start
  8. The bot will create the text and voice channels for everyone, and assign permissions for these rooms.
  9. If you need to set up a finals room, use the !finals command.
    !finals @Reader Team1, Team2
  10. To end the tournament, use !end
    !end

TODO: Add unit testing for message handling. Unless we add wrapper classes for SocketMessage/BaseSocketClient/etc., this will have to wait until Discord.Net supports interfaces for these.

TODO: Add persistence of tournaments, which requires using a database and making some changes to the basic classes (Reader, Team, etc.) to distinguish between database and Discord IDs. This would be a major change (v2)

TODO: Track member name changes so we can update reader/player names.

TODO: Have an option to allow for more automation, that would enable:
  - !win <team> in the channel would track who won that game, and would grant them permissions for the next room.
  - Tracking player buzzes. Either use a queue and clear on a score (0/10/15, etc.), or store a timestamp of the last buzz in the channel and mention the user if it's been long enough.
  - Longer term, potentially look at merging with the tournament runner and tracking all of the events.
  - This would be a major change (v3?)

TODO: Look into integrating with the Google Sheets API so we can listen to OphirStats updates and have the bot update point totals/TU numbers when the stats are updated.
