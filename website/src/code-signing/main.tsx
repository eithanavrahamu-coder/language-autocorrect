import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import CodeSigning from './CodeSigning.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <CodeSigning />
  </StrictMode>,
);
