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


Starting a tournament:
  1. Create your tournament by adding yourself as a TD to a tournament.
    !addTD @my_user <My tournament name>
  2. Start the setup phase with !setup
    !setup
  3. Add the readres with !addReader
    !addReader @reader_user
  4. Add teams with !addTeam
    !addTeam <team>
  5. Add players to teams with !addPlayer. Players can also join teams with !joinTeam. If they need to leave, use !removePlayer or !leaveTeam.
    !addPlayer @player_user <team>
    !joinTeam <team>
  6. Set the number of round robins to play with !setRoundRobins.
    !setRoundRobins <number of round robins>
  7. Start the tournament with !start.
    !start
  8. The bot will create the text and voice channels for everyone, and assign permissions for these rooms.


TODO: Figure out what's causing the throttling, and try to space out requests to avoid it.
  - One way to remove throttling is to be smart about round robins and just make rooms for team/pair matchups.
    - Downside with this approach is that this is harder for readers.
  - We could make the rooms channel categories, give readers access to that category, and then give teams permissions to individual channels in that category. We would have to set the category channel roles first, since child channels get "de-synchronized" and will no longer copy permissions of the parent category.
TODO: Move to using DMs for setup and joining teams, to keep the general channel less noisy.
TODO: Add range commands for addteams/addplayers (tricky thing with the latter: how do you take the list of mentions and the list of teams? Is the parser smart enough?)
TODO: Add helper method to clean up all of the bad channels?
TODO: Add support for finals
TODO: Add multi-guild support
TODO: Write guide for how to start a tournament as a TD
TODO: Add descriptions for parameters
TODO: Add persistence of tournaments
TODO: Make one role per team, and one role per reader, and assign those roles to the channels.
TODO: Have an option to allow for more automation, that would enable:
  - !win <team> in the channel would track who won that game, and would grant them permissions for the next room.
  - Tracking player buzzes. Either use a queue and clear on a score (0/10/15, etc.), or store a timestamp of the last buzz in the channel and mention the user if it's been long enough.
  - Longer term, potentially look at merging with the tournament runner and tracking all of the events.
  