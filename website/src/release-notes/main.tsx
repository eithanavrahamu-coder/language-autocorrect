import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import ReleaseNotes from './ReleaseNotes.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ReleaseNotes />
  </StrictMode>,
);
