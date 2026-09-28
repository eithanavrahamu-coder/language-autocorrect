import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import Downloads from './Downloads.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Downloads />
  </StrictMode>,
);
