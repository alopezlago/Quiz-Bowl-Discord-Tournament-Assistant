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
AddReactions
PrioritySpeaker (256)


Starting a tournament:
  1. Create your tournament by adding yourself as a TD to a tournament.
    !addTD @my_user <My tournament name>
  2. Start the setup phase with !setup
    !setup
  3. Add the readres with !addReader
    !addReader @reader_user
  4. Add teams with !addTeams
    !addTeams <comma-separated list of teams>
  5. Add players to teams with !addPlayer. Players can also join teams with !joinTeam. If they need to leave, use !removePlayer or !leaveTeam.
    !addPlayer @player_user <team>
    !joinTeam <team>
  6. Set the number of round robins to play with !setRoundRobins.
    !setRoundRobins <number of round robins>
  7. Start the tournament with !start.
    !start
  8. The bot will create the text and voice channels for everyone, and assign permissions for these rooms.


TODO: Add support for finals
TODO: Add multi-guild support
TODO: Write guide for how to start a tournament as a TD
TODO: Add persistence of tournaments
TODO: Have an option to allow for more automation, that would enable:
  - !win <team> in the channel would track who won that game, and would grant them permissions for the next room.
  - Tracking player buzzes. Either use a queue and clear on a score (0/10/15, etc.), or store a timestamp of the last buzz in the channel and mention the user if it's been long enough.
  - Longer term, potentially look at merging with the tournament runner and tracking all of the events.
  