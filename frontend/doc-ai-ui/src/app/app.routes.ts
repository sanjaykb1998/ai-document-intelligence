import { Routes } from '@angular/router';
import { UploadComponent } from './upload/upload';

export const routes: Routes = [
  { path: 'upload', component:  UploadComponent },
  { path: '', redirectTo: 'upload', pathMatch: 'full' }
];