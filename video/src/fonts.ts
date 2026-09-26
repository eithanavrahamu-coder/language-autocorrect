import '@fontsource-variable/bricolage-grotesque/standard.css';
import '@fontsource-variable/onest/index.css';
import { continueRender, delayRender } from 'remotion';

// Frames are taken only once the website's fonts are in, in every script the video uses them for.
const waiting = delayRender('Loading fonts');
Promise.all([
  document.fonts.load('700 100px "Bricolage Grotesque Variable"', 'Forgot to switch keyboards?'),
  document.fonts.load('500 100px "Onest Variable"', 'hello there'),
  document.fonts.load('600 100px "Onest Variable"', 'Download привет'),
  document.fonts.load('400 100px "Onest Variable"', 'Type in all your languages'),
])
  .then(() => continueRender(waiting))
  .catch(err => {
    console.error(err);
    continueRender(waiting);
  });
