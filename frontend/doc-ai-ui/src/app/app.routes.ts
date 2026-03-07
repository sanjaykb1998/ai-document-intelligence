import { Routes } from '@angular/router';
import { UploadComponent } from './upload/upload';
import { SignupComponent } from './auth/signup/signup';
import { LoginComponent } from './auth/login/login';
import { EntryComponent } from './auth/entry/entry';
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  { path: '', component: EntryComponent },
  { path: 'signup', component: SignupComponent },
  { path: 'login', component: LoginComponent },
  { path: 'upload', component:  UploadComponent , canActivate: [authGuard]},
  { path: '**', redirectTo: '' }
];